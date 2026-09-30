#!/usr/bin/env python3
"""Extract payload-byte totals and the executed-test count from one TRX file.

Usage: extract-payload-bytes.py <trx-file> <payload-log> <count-log>

Writes {className: {category: bytes}} to <payload-log> and {"executedTests": N}
to <count-log>, both without a BOM. A missing TRX yields empty results and
exit 0, because a namespace that produced no TRX must not fail the run.
"""

import json
import os
import re
import sys
import xml.etree.ElementTree as ET

PATTERN = re.compile(r'^\s*(.+?):\s+([a-z0-9_]+):\s+(\d+)\s+bytes\s*$')


def local_name(tag):
    return tag.rsplit('}', 1)[-1]


def children(parent, name):
    return (e for e in parent if local_name(e.tag) == name)


def descendants(parent, name):
    return (e for e in parent.iter() if local_name(e.tag) == name)


def extract(trx_file):
    payload_data = {}
    executed_tests = 0

    print(f"Checking for TRX file at: {trx_file}")
    if not os.path.isfile(trx_file):
        print("TRX file not found")
        return payload_data, executed_tests

    print(f"TRX file found. Size: {os.path.getsize(trx_file)} bytes")
    root = ET.parse(trx_file).getroot()

    for output in descendants(root, 'Output'):
        for text_messages in children(output, 'TextMessages'):
            for message in children(text_messages, 'Message'):
                for line in re.split(r'\r?\n', message.text or ""):
                    match = PATTERN.match(line)
                    if not match:
                        continue
                    bucket = payload_data.setdefault(match.group(1).strip(), {})
                    category = match.group(2)
                    bucket[category] = bucket.get(category, 0) + int(match.group(3))

    print(f"Found {len(payload_data)} payload log entries")

    # 'executed' counts tests that ran with any outcome, excluding skipped.
    counters = next(
        (c for s in descendants(root, 'ResultSummary') for c in children(s, 'Counters')),
        None,
    )
    if counters is not None and counters.get('executed'):
        executed_tests = int(counters.get('executed'))

    return payload_data, executed_tests


def write_json(path, data):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f)


def main(argv):
    if len(argv) != 4:
        print(__doc__.strip(), file=sys.stderr)
        return 2

    trx_file, payload_log, count_log = argv[1], argv[2], argv[3]
    payload_data, executed_tests = extract(trx_file)

    print(f"Executed test count: {executed_tests}")
    write_json(payload_log, payload_data)
    write_json(count_log, {"executedTests": executed_tests})

    if payload_data:
        print(f"Extracted payload logs to {payload_log}")
        print("Content:")
        print(json.dumps(payload_data))
    else:
        print("No payload byte logs found in TRX file")

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
