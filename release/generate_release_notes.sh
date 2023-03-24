#!/bin/bash

# Variables
version="$1"
trimmed_version=$(echo "$version" | sed 's/^v//')
template="release_notes_template.md"
output="release_notes_${version}.md"
date=$(date +"%Y-%m-%d")

# Define a function to get the previous tag with the desired naming format
get_prev_version() {
  local current_version="$1"
  local tag_pattern="v[0-9]*.[0-9]*.[0-9]*"
  local potential_prev_version=$(git describe --abbrev=0 --match "${tag_pattern}" --tags "${current_version}^")
  echo "${potential_prev_version#refs/tags/}"
}

prev_version=$(get_prev_version "$version")
echo "Previous release version: $prev_version"

# TODO: get this list from the output of "Release Please"
# Get the list of commits between the two versions
changelog=$(git log --pretty=format:"- %s" "${prev_version}..${version}")
changelog=$(echo "$changelog" | sed 's/\//\\\//g')

# TODO: capture these fields from the "Release Please" results
feature_summary_list="'Feature 1', 'Feature 2', 'Feature 3'"
bug_summary_list="'Bug 1', 'Bug 2', 'Bug 3'"
security_summary_list="'Security 1', 'Feature 2', 'Feature 3'"

# TODO: get checksum content from the deploy artifacts once this is in an action (reference existing action for details)
checksum_content="This is some sample checksum output!"

# Replace placeholders in the template with actual values
sed -e "s/{version}/${trimmed_version}/g" \
    -e "s/{date}/${date}/g" \
    -e "s/{feature_summary_list}/${feature_summary_list}/g" \
    -e "s/{bug_summary_list}/${bug_summary_list}/g" \
    -e "s/{security_summary_list}/${security_summary_list}/g" \
    -e "s/{checksum_content}/${checksum_content}/g" \
    -e "s/{changes_content}/${changelog//$'\n'/\\n}/g" \
    "$template" > "$output"

echo "Release notes for version $version generated in $output"