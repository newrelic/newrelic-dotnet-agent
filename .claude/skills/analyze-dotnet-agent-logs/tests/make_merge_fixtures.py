#!/usr/bin/env python3
"""Write rolled-file fixtures that exercise cross-file session merging.

Usage: make_merge_fixtures.py [output-dir]   (default: <this dir>/fixtures)

Creates two directories:
  merge-yes  one run rolled mid-session; nrlog.py must report ONE session
  merge-no   same pid resuming after 40 minutes; must report TWO sessions
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
BASE = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'fixtures')

BANNER = ("The New Relic .NET Agent v10.44.0 started (pid 5000) on app domain "
          "'/LM/W3SVC/9/ROOT'")
SHUTDOWN = ("The New Relic .NET Agent v10.44.0 has shutdown (pid 5000) on app domain "
            "'/LM/W3SVC/9/ROOT'")


def line(ts, level, pid, tid, msg):
    return '%s NewRelic %6s: [pid: %d, tid: %d] %s\n' % (ts, level, pid, tid, msg)


def write(dirname, files):
    out = os.path.join(BASE, dirname)
    os.makedirs(out, exist_ok=True)
    for name, lines in files.items():
        with open(os.path.join(out, name), 'w') as handle:
            handle.writelines(lines)
    print('wrote %s' % out)


older = [
    line('2026-08-18 10:00:00,000', 'INFO', 5000, 1, BANNER),
    line('2026-08-18 10:00:00,100', 'INFO', 5000, 1, 'Log level set to INFO'),
    line('2026-08-18 10:05:00,000', 'INFO', 5000, 1, 'Agent fully connected.'),
]

# rolled 2 seconds later: one continuous run
newer = [
    line('2026-08-18 10:05:02,000', 'INFO', 5000, 1,
         'Your New Relic Application Name(s): Rolled'),
    line('2026-08-18 10:09:00,000', 'INFO', 5000, 1, SHUTDOWN),
]
write('merge-yes', {'newrelic_agent_Rolled_001.log': older,
                    'newrelic_agent_Rolled.log': newer})

# resumes 40 minutes later with no banner: two separate sessions
gapped = [
    line('2026-08-18 10:45:00,000', 'INFO', 5000, 1,
         'Your New Relic Application Name(s): Later'),
    line('2026-08-18 10:46:00,000', 'INFO', 5000, 1, 'Agent fully connected.'),
]
write('merge-no', {'newrelic_agent_Rolled_001.log': older,
                   'newrelic_agent_Rolled.log': gapped})
