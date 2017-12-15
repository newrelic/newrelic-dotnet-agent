#
# PostMessageToChatRoom posts a message to the appropriate class of chatroom.
#
# As of 18Apr2016 the company moved from hipchat to Slack.
# The contents of the rooms in hipchat were migrated to Slack,
# but there was a change of names.
#
# This function will torque-convert the hipchat room name to the Slack room
# name, as we can't easily find all callers.
#
# In the quick effort to convert to Slack,
# we had to remove the generation of HTML
# from the call sites, and just embed URLs,
# and rely on Slack for extracting those URLs.
#
Function PostMessageToChatRoom(
  [string] $room_id,
  [string] $message,
  [string] $message_format,  # Ignored: "html" or "text"
  [string] $notify,          # Ignored: 0 or 1 (1==>message alert roomies)
  [string] $color)
{
  PostMessageToSlack $room_id $message $color
}

#
# Slack was activated corporate wide on or about Friday 15Apr2016
# See:
#   https://docs.google.com/document/d/1l7YtdtS0wF8HF3St6zU4FoZzHC2RGaKVO7cNaSB4a0Q/edit
# See:
#   https://api.slack.com/docs/attachments
#
# Unlike the Hipchat poster, this Slack poster doesn't
# check message_format nor
Function PostMessageToSlack(
  [string] $room_id,
  [string] $message,
  [string] $color)
{
  # Convert from color names empirically used by the existing call sites
  # into the Slack color namespace.
  # (These colors are taken from the legacy hipchat color name space.)
  if ($color -eq"red") {
    $color = "danger"
  } elseif ($color -eq"yellow") {
    $color = "warning"
  } elseif ($color -eq "green") {
    $color = "good"
  } elseif ($color -eq "gray") {
    $color = "#d3d3d3"
  }

  #
  # A field will be displayed as a table inside the message attachment.
  #
  $field = @{
    title = "Jenkins Build Bot reporting in";
    short = $False;  # title is not long enough to be displayed side-by-side
  }

  $attachment = @{
    # fallback is a required plain text summary
    # TODO: add more meat to this?
    fallback = "Jenkins Build Bot reporting in";  # required plain text summary

    color = $color;

    # title = ...;
    # title_link = ...;

    # pretext = "$message";  # optional text appears above the attachment block

    # text may contain slack message markup
    # See: https://api.slack.com/docs/formatting
    text = "$message"; # main text in a message attachment

    # image_url = ...;
    # thumb_url = ...;

    fields = @($field);

    #
    # mrkdwn_in = @("pretext", "text", "fields")
    #
  }

  $data = @{
    channel = $room_id;
    username = "Jenkins";
    # text = "text appearing before all the attachments";
    attachments = @($attachment);
  }

  $body = ConvertTo-Json -InputObject $data -Depth 32

  #
  # This web hook was assigned to rrh on 15Apr2016 by IT/Damien Quillan
  # as part of corporate migration from hipchat to slack.
  # It should be treated like a password, alas.
  #
  Invoke-RestMethod `
    -Uri "https://hooks.slack.com/services/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" `
    -ContentType "application/json" `
    -Method Post `
    -Body $body
}

Function TestPostMessage() {
  $chatRoomMessage = "Boo! development of chatRoomAPI.ps1"
  PostMessageToChatRoom "dotnet-agent" $chatRoomMessage "html" 0 "gray"  # TEST
}