Write-Host "Importing gitHub.psm1"
$ErrorActionPreference = "Stop"

$org = "dotNetAgent"
$main_repo = "dotnet_agent"
$baseUrl = "https://source.datanerd.us/api/v3"

function GetReference($ref)
{
    MakeAPIRequest "$baseUrl/repos/$org/$main_repo/git/refs/$ref"
}

function UpdateReference($ref, $sha)
{
    Write-Host "Fast-forwarding '$ref' to '$sha'"
    $uri = "$baseUrl/repos/$org/$main_repo/git/refs/$ref"
    $json = @{sha=$sha;force=$false} | ConvertTo-Json
    MakeAPIRequest $uri "PATCH" $json
}

function GetCommit($sha)
{
    MakeAPIRequest "$baseUrl/repos/$org/$main_repo/commits/$sha"
}

function CreatePullRequest($title, $head, $base)
{
    Write-Host "Generating Pull Request"
    $pulls = "$baseUrl/repos/$org/$main_repo/pulls"
    $json = @{title=$title;head=$head;base=$base} | ConvertTo-Json
    MakeAPIRequest $pulls "POST" $json
}

function CreateTag($tag, $message, $sha)
{
    Write-Host "Creating tag object '$tag' at '$sha'."
    $urlTags = "$baseUrl/repos/$org/$main_repo/git/tags"
    $jsonTag = @{tag=$tag;message=$message;object=$sha;type="commit"} | ConvertTo-Json
    $responseJson = MakeAPIRequest $urlTags "POST" $jsonTag
    $tagSha = $responseJson.sha

    Write-Host "Creating ref to '$tag' using '$tagSha'."
    $urlRefs = "$baseUrl/repos/$org/$main_repo/git/refs"
    $jsonRef = @{ref="refs/tags/$tag";sha=$tagSha} | ConvertTo-Json
    MakeAPIRequest $urlRefs "POST" $jsonRef
}

function CreateBranch($branchName, $sha)
{
    Write-Host "Creating branch '$branchName' from '$sha'."
    $url = "$baseUrl/repos/$org/$main_repo/git/refs"
    $json = @{ref="refs/heads/$branchName";sha=$sha} | ConvertTo-Json
    MakeAPIRequest $url "POST" $json
}

function MergeBranch($base, $head)
{
    Write-Host "Merging '$head' into '$base'."
    $url = "$baseUrl/repos/$org/$main_repo/merges"
    $json = @{base=$base;head=$head} | ConvertTo-Json
    MakeAPIRequest $url "POST" $json
}

function FindPullRequestsMergedFollowingCommit($sha)
{
    Write-Host "Finding Pull Requests merged to 'dev' since '$sha'"

    $commit = MakeAPIRequest "$baseUrl/repos/$org/dotnet_agent/commits/$sha"
    $since = $commit.commit.author.date

    Write-Host "- Searching PR's merged after '$since'."
    $closedIssues = MakeAPIRequest "$baseUrl/repos/$org/$main_repo/issues?since=$since&state=closed"
    $pullRequestsMissingRiskLabels = New-Object 'System.Collections.Generic.List[String]';

    foreach ($closedIssue in $closedIssues)
    {
        if ($closedIssue.pull_request)
        {
            $pullRequest = MakeAPIRequest $closedIssue.pull_request.url

            if ($pullRequest.merged -eq "True" -and $pullRequest.base.ref -eq "dev")
            {
                $issueUrl = $pullRequest._links.issue.href
                $issue = MakeAPIRequest $issueUrl
                
                if ($issue.labels | Where { $_.name.Contains("risk") })
                {
                    $risk = ($issue.labels | Where { $_.name.Contains("risk") }).name.Replace("risk:", [String]::Empty)
                    $closedPullRequests += "$($pullRequest.number) ($($pullRequest.user.login)) [$risk]: $($pullRequest.title)`n"
                }
                else
                {
                    Write-Host "-- WARNING: $($pullRequest.number) does not have a 'risk' label set"
					$pullRequestsMissingRiskLabels.Add($pullRequest.number);
                }
            }
        }
    }

    Write-Host "Pull Requests merged to 'dev' since '$since':`n$closedPullRequests"

	if($pullRequestsMissingRiskLabels -ne $null) {
		Send-MailMessage -SmtpServer pdx-dc-boss -Subject "Action Required: One or more PRs need attention" -From "**REDACTED**" -To "**REDACTED**" -Body "The following PRs are missing risk labels:`n$pullRequestsMissingRiskLabels"
	}	
}

function CalculateReleaseRisk([string] $sha)
{
    Write-Host "Calculating risk of next release..."
    Write-Host "- Gathering commit information for '$sha'"
    $commit = MakeAPIRequest "$baseUrl/repos/$org/dotnet_agent/commits/$sha"

    $since = $commit.commit.author.date
    Write-Host "- Searching PR's merged after '$since'."
    
    # Default risk level to low
    $riskLevel = "LOW"

    # "Every pull request is an issue"
    # "If the issue is not a pull request, the response omits the 'pull_request' attribute"
    $issuesMedium = MakeAPIRequest "$baseUrl/repos/$org/$main_repo/issues?since=$since&state=closed&labels=risk:medium"
    $issuesHigh = MakeAPIRequest "$baseUrl/repos/$org/$main_repo/issues?since=$since&state=closed&labels=risk:high"

    if ($issuesMedium)
    {
        Write-Host "- Found $($issuesMedium.Count) medium risk issue(s)."

        foreach ($issueMedium in $issuesMedium)
        {
            if ($issueMedium.pull_request)
            {
                $pullRequest = MakeAPIRequest $issueMedium.pull_request.url

                if ($pullRequest.merged -eq "True" -and $pullRequest.base.ref -eq "dev")
                {
                    Write-Host "$($pullRequest.url) is risk level MEDIUM"
                    $riskLevel = "MEDIUM"
                }
            }
        }
    }
    elseif ($issuesHigh)
    {
        Write-Host "- Found $($issuesHigh.Count) high risk issue(s)."

        foreach ($issueHigh in $issuesHigh)
        {
            if ($issueHigh.pull_request)
            {
                $pullRequest = MakeAPIRequest $issueHigh.pull_request.url
                if ($pullRequest.merged -eq "True" -and $pullRequest.base.ref -eq "dev")
                {
                    Write-Host "$($pullRequest.url) is risk level HIGH"
                    $riskLevel = "HIGH"
                }
            }
        }
    }
    else
    {
        Write-Host "- Found no merged PR's with a MEDIUM or HIGH risk level, defaulting to LOW"
    }
    Write-Host "- Risk level is $riskLevel"
    return $riskLevel
}

function GetRepoNames
{
	$repoNames = @()

	$repoDetails = MakeAPIRequest "$baseUrl/orgs/$org/repos"	
	$repoDetails | Foreach { $repoNames += $_.name }
    
	return $repoNames
}

function AddPRChecklist
{
    $events = MakeAPIRequest "$baseUrl/repos/$org/$main_repo/events"
    $pullRequestEvents = $events | Where { $_.type -eq "PullRequestEvent" -and $_.payload.action -eq "opened" -and $_.payload.pull_request.head.sha -eq $env:ghprbActualCommit }

    if ($pullRequestEvents)
    {
        Write-Host "Pull request event for 'opened' found for commit sha: $env:ghprbActualCommit."
        $pullRequestEvents | Foreach { Write-Host $_.id $_.payload.pull_request.base.sha $_.payload.number $_.payload.pull_request.comments_url; $commentsUrl = $_.payload.pull_request.comments_url }
        Write-Host "Comments Url: $commentsUrl"

        $comments = MakeAPIRequest $commentsUrl
        $checklistComment = $comments | Where { $_.body.Contains("PR Checklist") }

        if (!$checklistComment)
        {
            Write-Host "Checklist has NOT been added to PR. Adding checklist to pull request"
            $markdown = "#### PR Checklist:`n##### Assign labels`n - [ ] ``risk:```n - [ ] ``priority:``"
            $json = @{body=$markdown} | ConvertTo-Json
            MakeAPIRequest $commentsUrl "POST" $json
        }
        else
        {
            Write-Host "Checklist has already been added to the PR."
        }
    }
    else
    {
        Write-Host "No 'PullRequestEvents' found with an action 'opened', checklist will NOT be added to the PR"
    }
}

function MakeAPIRequest([string] $uri, [string] $httpMethod = "GET", $json = $null)
{
    Write-Host "- Making request to '$uri' using '$httpMethod'"
    if ($httpMethod -eq 'GET')
    {
        return Invoke-WebRequest -uri $uri -Method Get -Headers @{Authorization="Token $env:GHEToken"} | ConvertFrom-Json
    }
    elseif($httpMethod -eq 'DELETE')
    {
        return Invoke-WebRequest -uri $uri -Method Delete -Headers @{Authorization="Token $env:GHEToken"} | ConvertFrom-Json
    }
    else
    {
        return Invoke-WebRequest -uri $uri -Method $httpMethod -Headers @{Authorization="Token $env:GHEToken"} -Body $json | ConvertFrom-Json
    }
}

Export-ModuleMember -Function CreateTag
Export-ModuleMember -Function GetCommit
Export-ModuleMember -Function MakeAPIRequest