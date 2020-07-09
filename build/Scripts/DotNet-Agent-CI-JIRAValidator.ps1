$username = $env:Username
$password = $env:Password
. .\windows\common\powershell\jiraAPI.ps1
. .\windows\common\powershell\jiraActions.ps1

# Check work items for 'Customer-facing release note', and 'Support-facing release note'
$releaseNotesFailures = CheckWorkItemsForReleaseNotes

# Check work items for 'fixVersion'
$fixVersionFailures = CheckWorkItemsForFixVersion

# Check work items for linked items -- DISABLED 8/19/15. We no longer care about this check.
# $linkedItemsFailures = CheckWorkItemsForLinkedItems

# Sub tasks
$subTaskFailures = CheckSubTasks

# Generate customer-facing release notes for unreleased work items
$customerFacingReleaseNotes = ParseCustomerFacingReleaseNotes

Write-Host $releaseNotesFailures
Write-Host $fixVersionFailures
Write-Host $linkedItemsFailures
#Write-Host $customerFacingReleaseNotes

if ($releaseNotesFailures -or $fixVersionFailures -or $linkedItemsFailures -or $subTaskFailures) {
    Send-MailMessage -SmtpServer pdx-dc-boss -Subject "Action Required: One or more JIRA issues need attention" -From "**REDACTED**" -To "**REDACTED**" -Body "The following JIRA issues are missing one or more pieces of required information:`n$releaseNotesFailures`n$fixVersionFailures`n$linkedItemsFailures`n$subTaskFailures"
    exit 1
}