#!/usr/bin/env python3
"""Synthetic playbook sets: one field-tier playbook, 40 that never match, 40 that always do.

Usage: make_playbook_fixtures.py [output-dir]   (default: <this dir>/fixtures/playbooks)

  field        one field-tier playbook, so the [field] label has a live test
  scale        40 playbooks whose signatures appear in no fixture: CLEAR collapses
  scale-match  40 playbooks that all match the Quiet session: MATCHED caps
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'fixtures', 'playbooks')

FIELD = '''---
id: 101
title: Field observed startup stall
tier: field
scope: managed
min_level: info
precedence: 80
signatures:
  - "Agent fully connected"
keywords: [connect, startup]
observed_in: "10.40.1"
---

**Symptom.** Placeholder for the field-tier test.

## Customer fix

Restart the application pool.

## Next ask

The application pool recycle settings.
'''

# tier field, not verified: validate_playbook holds verified ids at or below
# SHIPPED_ID_MAX (99), so a synthetic set numbered above 99 must be field tier
SCALE = '''---
id: %(id)d
title: Synthetic playbook %(id)d
tier: field
scope: managed
min_level: info
precedence: %(id)d
signatures:
  - "%(signature)s"
keywords: [synthetic]
observed_in: "10.54.0"
---

**Symptom.** Synthetic.

## Customer fix

Nothing.

## Next ask

Nothing.
'''

NEVER = 'synthetic signature %d never appears in a fixture'
ALWAYS = 'Log level set to'

os.makedirs(os.path.join(OUT, 'field'), exist_ok=True)
os.makedirs(os.path.join(OUT, 'scale'), exist_ok=True)
os.makedirs(os.path.join(OUT, 'scale-match'), exist_ok=True)

with open(os.path.join(OUT, 'field', '101-field-startup-stall.md'), 'w') as handle:
    handle.write(FIELD)

for number in range(200, 240):
    with open(os.path.join(OUT, 'scale', '%d-synthetic.md' % number), 'w') as handle:
        handle.write(SCALE % {'id': number, 'signature': NEVER % number})

for number in range(300, 340):
    with open(os.path.join(OUT, 'scale-match', '%d-synthetic.md' % number), 'w') as handle:
        handle.write(SCALE % {'id': number, 'signature': ALWAYS})

print('playbook fixtures written to %s' % OUT)
