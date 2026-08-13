#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT
mkdir -p "$test_root/bin" "$test_root/release"

cat > "$test_root/bin/az" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
echo "$*" >> "$AZURE_MOCK_LOG"
case "$*" in
  *"job start"*) echo migration-execution-1 ;;
  *"job execution show"*) echo Succeeded ;;
  *"revision list"*"contains(name,"*)
    app='app'
    for ((index=1; index <= $#; index++)); do
      test "${!index}" != --name || { next=$((index + 1)); app="${!next}"; }
    done
    echo "${app}--0123456789ab"
    ;;
  *"revision list"*)
    app='app'
    for ((index=1; index <= $#; index++)); do
      test "${!index}" != --name || { next=$((index + 1)); app="${!next}"; }
    done
    echo "${app}--previous"
    ;;
esac
MOCK
cat > "$test_root/bin/curl" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
echo "$*" >> "$CURL_MOCK_LOG"
MOCK
chmod +x "$test_root/bin/az" "$test_root/bin/curl"

digest='sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
jq -n --arg digest "$digest" '{schemaVersion:1,commit:"0123456789abcdef0123456789abcdef01234567",images:{api:("ghcr.io/example/api@"+$digest),web:("ghcr.io/example/web@"+$digest),worker:("ghcr.io/example/worker@"+$digest),migrator:("ghcr.io/example/migrator@"+$digest)}}' > "$test_root/release/release-manifest.json"

export PATH="$test_root/bin:$PATH"
export AZURE_MOCK_LOG="$test_root/azure.log"
export CURL_MOCK_LOG="$test_root/curl.log"
export DEPLOYMENT_ENVIRONMENT=staging
export AZURE_RESOURCE_GROUP=rg-test
export AZURE_CONTAINER_REGISTRY=registrytest
export AZURE_WEB_APP=web-test
export AZURE_API_APP=api-test
export AZURE_WORKER_APP=worker-test
export AZURE_MIGRATION_JOB=migration-test
export APPLICATION_URL=https://test.example
export GHCR_USERNAME=workflow
export GHCR_TOKEN=masked-test-token
export GITHUB_OUTPUT="$test_root/github-output"

bash "$root/scripts/deploy-azure.sh" "$test_root/release/release-manifest.json"
record="$test_root/release/deployment-record.json"
jq -e '.environment == "staging" and .migrationExecution == "migration-execution-1" and .previousRevisions.web == "web-test--previous" and .previousRevisions.api == "api-test--previous"' "$record" >/dev/null
grep -q 'job start' "$AZURE_MOCK_LOG"
grep -q 'api-test--0123456789ab=100' "$AZURE_MOCK_LOG"
grep -q 'web-test--0123456789ab=100' "$AZURE_MOCK_LOG"
grep -q 'https://test.example/health' "$CURL_MOCK_LOG"

: > "$AZURE_MOCK_LOG"
bash "$root/scripts/rollback-azure.sh" "$record"
grep -q 'api-test--previous=100' "$AZURE_MOCK_LOG"
grep -q 'web-test--previous=100' "$AZURE_MOCK_LOG"
echo 'Deployment and rollback script tests passed.'
