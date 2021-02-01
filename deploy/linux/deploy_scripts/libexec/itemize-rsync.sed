# remove preamble
/building\|receiving file list/ d

# ignore files and directories with no changes
/^\./ d

# new file
s/^[<>]f+++++++++\s*\(.*\)$/A \1/

# new directory
s/^cd+++++++++\s*\(.*\)$/C \1/

# modified file, link or directory
s/^[<>ch]..........\s*\(.*\)$/M \1/

# deletion
s/^\*deleting\s*\(.*\)/D \1/

/^[ACDMS] debian/ {
  # debian production repo
  s!debian/dists/newrelic/.*$![production/debian]\x09&!

  # debian other repo
  s!debian/dists/newrelic-\([^/]*\)/.*$![\1/debian]\x09&!

  b
}

/^[ACDMS] pub/ {
  # redhat production repo
  s!pub/newrelic/.*$![production/redhat]\x09&!

  # redhat other repo
  s!pub/newrelic-\([^/]*\)/.*$![\1/redhat]\x09&!

  b
}

/^[ACDMS] php_agent/ {
  tp
  :p
  
  s!php_agent/release/.*$![production/php_agent]\x09&!
  t

  s!php_agent/\([^/]*\)/.*$![\1/php_agent]\x09&!
  b
}

/^[ACDMS] server_monitor/ {
  ts
  :s

  s!server_monitor/release/.*$![production/server_monitor]\x09&!
  t

  s!server_monitor/\([^/]*\)/.*$![\1/server_monitor]\x09&!
  b
}
