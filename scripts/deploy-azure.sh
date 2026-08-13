#!/usr/bin/env bash
set -euo pipefail

manifest_path="${1:?release manifest path is required}"
: "${DEPLOYMENT_ENVIRONMENT:?}"
: "${AZURE_RESOURCE_GROUP:?}"
: "${AZURE_CONTAINER_REGISTRY:?}"
: "${AZURE_WEB_APP:?}"
: "${AZURE_API_APP:?}"
: "${AZURE_WORKER_APP:?}"
: "${AZURE_MIGRATION_JOB:?}"
: "${APPLICATION_URL:?}"
: "${GHCR_USERNAME:?}"
: "${GHCR_TOKEN:?}"

for command in az jq curl; do command -v "$command" >/dev/null; done
jq -e '.schemaVersion == 1 and (.commit | test("^[0-9a-f]{40}$"))' "$manifest_path" >/dev/null
for component in api web worker migrator; do
  jq -er --arg component "$component" '.images[$component] | select(test("@sha256:[0-9a-f]{64}$"))' "$manifest_path" >/dev/null
done

commit="$(jq -r .commit "$manifest_path")"
suffix="${commit:0:12}"
record_path="$(dirname "$manifest_path")/deployment-record.json"

declare -A imported
for component in api web worker migrator; do
  source_image="$(jq -r --arg component "$component" '.images[$component]' "$manifest_path")"
  source_digest="${source_image##*@}"
  target_repository="reponav-ai-${component}"
  az acr import --name "$AZURE_CONTAINER_REGISTRY" --source "$source_image" --image "${target_repository}:${commit}" --username "$GHCR_USERNAME" --password "$GHCR_TOKEN" --force --only-show-errors
  imported[$component]="${AZURE_CONTAINER_REGISTRY}.azurecr.io/${target_repository}@${source_digest}"
done

az containerapp job update --name "$AZURE_MIGRATION_JOB" --resource-group "$AZURE_RESOURCE_GROUP" --image "${imported[migrator]}" --only-show-errors >/dev/null
execution="$(az containerapp job start --name "$AZURE_MIGRATION_JOB" --resource-group "$AZURE_RESOURCE_GROUP" --query name -o tsv)"
for _ in $(seq 1 90); do
  status="$(az containerapp job execution show --name "$AZURE_MIGRATION_JOB" --resource-group "$AZURE_RESOURCE_GROUP" --job-execution-name "$execution" --query properties.status -o tsv)"
  case "$status" in
    Succeeded) break ;;
    Failed|Stopped|Degraded) echo "Migration execution $execution failed with $status" >&2; exit 1 ;;
  esac
  sleep 10
done
test "${status:-}" = Succeeded

previous_web="$(az containerapp revision list --name "$AZURE_WEB_APP" --resource-group "$AZURE_RESOURCE_GROUP" --query "[?properties.active].name | [0]" -o tsv)"
previous_api="$(az containerapp revision list --name "$AZURE_API_APP" --resource-group "$AZURE_RESOURCE_GROUP" --query "[?properties.active].name | [0]" -o tsv)"

az containerapp update --name "$AZURE_API_APP" --resource-group "$AZURE_RESOURCE_GROUP" --image "${imported[api]}" --revision-suffix "$suffix" --only-show-errors >/dev/null
api_revision="$(az containerapp revision list --name "$AZURE_API_APP" --resource-group "$AZURE_RESOURCE_GROUP" --query "[?contains(name, '$suffix')].name | [0]" -o tsv)"
az containerapp update --name "$AZURE_WEB_APP" --resource-group "$AZURE_RESOURCE_GROUP" --image "${imported[web]}" --revision-suffix "$suffix" --only-show-errors >/dev/null
web_revision="$(az containerapp revision list --name "$AZURE_WEB_APP" --resource-group "$AZURE_RESOURCE_GROUP" --query "[?contains(name, '$suffix')].name | [0]" -o tsv)"

rollback() {
  echo 'Smoke test failed; restoring prior web/API revisions.' >&2
  test -z "$previous_api" || az containerapp ingress traffic set --name "$AZURE_API_APP" --resource-group "$AZURE_RESOURCE_GROUP" --revision-weight "${previous_api}=100" --only-show-errors >/dev/null
  test -z "$previous_web" || az containerapp ingress traffic set --name "$AZURE_WEB_APP" --resource-group "$AZURE_RESOURCE_GROUP" --revision-weight "${previous_web}=100" --only-show-errors >/dev/null
}
trap rollback ERR

az containerapp ingress traffic set --name "$AZURE_API_APP" --resource-group "$AZURE_RESOURCE_GROUP" --revision-weight "${api_revision}=100" --only-show-errors >/dev/null
az containerapp ingress traffic set --name "$AZURE_WEB_APP" --resource-group "$AZURE_RESOURCE_GROUP" --revision-weight "${web_revision}=100" --only-show-errors >/dev/null
curl --fail --silent --show-error --retry 12 --retry-delay 10 --retry-all-errors "${APPLICATION_URL%/}/health" >/dev/null
curl --fail --silent --show-error --retry 12 --retry-delay 10 --retry-all-errors "${APPLICATION_URL%/}/" >/dev/null

az containerapp update --name "$AZURE_WORKER_APP" --resource-group "$AZURE_RESOURCE_GROUP" --image "${imported[worker]}" --revision-suffix "$suffix" --only-show-errors >/dev/null
worker_revision="$(az containerapp revision list --name "$AZURE_WORKER_APP" --resource-group "$AZURE_RESOURCE_GROUP" --query "[?contains(name, '$suffix')].name | [0]" -o tsv)"
trap - ERR

jq -n --arg environment "$DEPLOYMENT_ENVIRONMENT" --arg commit "$commit" --arg migration "$execution" \
  --arg web "$web_revision" --arg api "$api_revision" --arg worker "$worker_revision" \
  --arg previousWeb "$previous_web" --arg previousApi "$previous_api" \
  '{environment:$environment,commit:$commit,migrationExecution:$migration,revisions:{web:$web,api:$api,worker:$worker},previousRevisions:{web:$previousWeb,api:$previousApi}}' > "$record_path"

echo "application-url=${APPLICATION_URL}" >> "$GITHUB_OUTPUT"
