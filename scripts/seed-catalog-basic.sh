#!/usr/bin/env bash

set -euo pipefail

BASE_URL="${1:-https://homesuite.nestlerlabs.at}"
API_BASE="${BASE_URL%/}/api/catalog"

create_item() {
  local payload="$1"

  curl --fail --silent --show-error \
    -X POST "$API_BASE" \
    -H 'Content-Type: application/json' \
    --data "$payload"
}

create_item '{"name":"Tomaten","defaultUnit":"Stk","category":"Gemuese","searchTerm":"Tomaten","brandHint":"","isStaple":true}'
create_item '{"name":"Tomatenmark","defaultUnit":"Tube","category":"Vorrat","searchTerm":"Tomatenmark","brandHint":"","isStaple":true}'
create_item '{"name":"Zwiebeln","defaultUnit":"kg","category":"Gemuese","searchTerm":"Zwiebeln","brandHint":"","isStaple":true}'
create_item '{"name":"Knoblauch","defaultUnit":"Knolle","category":"Gemuese","searchTerm":"Knoblauch","brandHint":"","isStaple":true}'
create_item '{"name":"Kartoffeln","defaultUnit":"kg","category":"Gemuese","searchTerm":"Kartoffeln","brandHint":"","isStaple":true}'
create_item '{"name":"Milch","defaultUnit":"l","category":"Kuehlregal","searchTerm":"Milch","brandHint":"","isStaple":true}'
create_item '{"name":"Butter","defaultUnit":"Packung","category":"Kuehlregal","searchTerm":"Butter","brandHint":"","isStaple":true}'
create_item '{"name":"Eier","defaultUnit":"Stk","category":"Kuehlregal","searchTerm":"Eier","brandHint":"","isStaple":true}'
create_item '{"name":"Nudeln","defaultUnit":"Packung","category":"Vorrat","searchTerm":"Nudeln","brandHint":"","isStaple":true}'
create_item '{"name":"Reis","defaultUnit":"kg","category":"Vorrat","searchTerm":"Reis","brandHint":"","isStaple":true}'
create_item '{"name":"Mehl","defaultUnit":"kg","category":"Backen","searchTerm":"Mehl","brandHint":"","isStaple":true}'
create_item '{"name":"Olivenoel","defaultUnit":"Flasche","category":"Vorrat","searchTerm":"Olivenoel","brandHint":"","isStaple":true}'
