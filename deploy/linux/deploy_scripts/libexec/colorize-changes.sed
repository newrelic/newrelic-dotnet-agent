# colorize an itemized list of changes where the first column
# indicates the type of change

# add/create = yellow
/^A\|C / {
  s/^/\x1b[33m/
  s/$/\x1b[m/
}

# delete = red
/^D / {
  s/^/\x1b[31m/
  s/$/\x1b[m/
}

# modify = green
/^M / {
  s/^/\x1b[32m/
  s/$/\x1b[m/
}
