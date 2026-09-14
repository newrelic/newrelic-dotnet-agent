#!/usr/bin/env python3
"""Re-record tests/golden/triage-basic.txt, normalized the way the test compares it.

Usage: python tests/record_golden.py   (run tests/make_fixtures.py first)
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', 'scripts'))

import nrlog
import test_nrlog

TARGET = os.path.join(HERE, 'golden', 'triage-basic.txt')

if not os.path.isdir(test_nrlog.FIXTURES):
    sys.exit('run python tests/make_fixtures.py first')

lines = nrlog.triage_report(path=test_nrlog.FIXTURES, file='Quiet', session=None,
                            playbooks=None, max_matched=5, evidence=2,
                            width=200, no_version=True)
text = test_nrlog.normalize('\n'.join(lines))
os.makedirs(os.path.dirname(TARGET), exist_ok=True)
with open(TARGET, 'w', encoding='utf-8', newline='\n') as handle:
    handle.write(text + '\n')
print('recorded %s: %d lines' % (TARGET, len(lines)))
print(text)
