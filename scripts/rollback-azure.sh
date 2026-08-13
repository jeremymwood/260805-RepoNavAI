#!/usr/bin/env bash
set -euo pipefail

record_path="${1:?deployment record path is required}"
: "${AZURE_RESOURCE_GROUP:?}"
: "${AZURE_WEB_APP:?}"
: "${AZURE_API_APP:?}"

web_revision="$(jq -er '.previousRevisions.web | select(length > 0)' "$record_path")"
api_revision="$(jq -er '.previousRevisions.api | select(length > 0)' "$record_path")"

az containerapp ingress traffic set --name "$AZURE_API_APP" --resource-group "$AZURE_RESOURCE_GROUP" --revision-weight "${api_revision}=100" --only-show-errors
az containerapp ingress traffic set --name "$AZURE_WEB_APP" --resource-group "$AZURE_RESOURCE_GROUP" --revision-weight "${web_revision}=100" --only-show-errors
