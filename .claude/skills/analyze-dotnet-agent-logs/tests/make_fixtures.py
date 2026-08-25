#!/usr/bin/env python3
"""Write synthetic agent and profiler logs for regression-testing nrlog.py.

Usage: make_fixtures.py [output-dir]   (default: <this dir>/fixtures/logs)

Every line matches a format verified against agent source. The license key is
planted deliberately so a redaction check can grep the derived files for it.
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'fixtures', 'logs')
os.makedirs(OUT, exist_ok=True)

KEY = 'abcdefghij0123456789klmnopqrstuvwxyz' + 'NRAL'


def line(ts, level, pid, tid, msg):
    return '%s NewRelic %6s: [pid: %d, tid: %d] %s\n' % (ts, level, pid, tid, msg)


rolled = []
# head-truncated session for pid 8412: starts mid-run, no start banner
rolled.append(line('2026-08-18 13:58:00,001', 'INFO', 8412, 1,
                   'Your New Relic Application Name(s): OldApp'))
rolled.append(line('2026-08-18 13:58:01,002', 'INFO', 8412, 1,
                   'The New Relic .NET Agent v10.44.0 has shutdown (pid 8412) on app domain '
                   "'/LM/W3SVC/1/ROOT'"))

with open(os.path.join(OUT, 'newrelic_agent_MyApp_001.log'), 'w') as f:
    f.writelines(rolled)

main = []
# full session: both banners, DEBUG level, collector payloads, pid 8412 restart
main.append(line('2026-08-18 14:03:11,123', 'INFO', 8412, 1,
                 'The New Relic .NET Agent v10.44.0 started (pid 8412) on app domain '
                 "'/LM/W3SVC/2/ROOT'"))
main.append(line('2026-08-18 14:03:11,130', 'INFO', 8412, 1, 'Log level set to DEBUG'))
main.append(line('2026-08-18 14:03:11,140', 'DEBUG', 8412, 1,
                 'Environment Variable NEW_RELIC_APP_NAME value: MyApp'))
main.append(line('2026-08-18 14:03:11,150', 'DEBUG', 8412, 1,
                 'Environment Variable NEW_RELIC_LICENSE_KEY is configured with a value. '
                 'Not logging potentially sensitive value'))
main.append(line('2026-08-18 14:03:12,000', 'FINEST', 8412, 5,
                 'Request(11111111-1111-1111-1111-111111111111): Invoking "preconnect"'))
main.append(line('2026-08-18 14:03:12,100', 'DEBUG', 8412, 5,
                 'Request(11111111-1111-1111-1111-111111111111): Invoked "preconnect" with : '
                 '[{"high_security":false}]'))
main.append(line('2026-08-18 14:03:12,200', 'DEBUG', 8412, 5,
                 'Request(11111111-1111-1111-1111-111111111111): Invocation of "preconnect" '
                 'yielded response : {"return_value":{"redirect_host":"collector-1.nr.com"}}'))
main.append(line('2026-08-18 14:03:13,000', 'DEBUG', 8412, 5,
                 'Request(22222222-2222-2222-2222-222222222222): Invoked "connect" with : '
                 '[{"pid":8412,"app_name":["MyApp"],'
                 '"settings":{"agent.license_key.configured":true}}]'))
main.append(line('2026-08-18 14:03:13,300', 'DEBUG', 8412, 5,
                 'Request(22222222-2222-2222-2222-222222222222): Invocation of "connect" '
                 'yielded response : {"return_value":{"agent_run_id":"123456789",'
                 '"collect_span_events":false,"max_payload_size_in_bytes":1000000}}'))
main.append(line('2026-08-18 14:03:13,400', 'INFO', 8412, 5,
                 'Agent MyApp connected to collector-1.nr.com:443'))
main.append(line('2026-08-18 14:03:13,500', 'INFO', 8412, 5, 'Agent fully connected.'))
# interleaved second pid inside the same range
main.append(line('2026-08-18 14:03:14,000', 'INFO', 9100, 1,
                 'The New Relic .NET Agent v10.44.0 started (pid 9100) on app domain '
                 "'/LM/W3SVC/3/ROOT'"))
main.append(line('2026-08-18 14:03:14,100', 'INFO', 9100, 1, 'Log level set to INFO'))
# error response, then an exception whose text carries the URI and the license key
main.append(line('2026-08-18 14:04:00,000', 'DEBUG', 8412, 5,
                 'Request(33333333-3333-3333-3333-333333333333): Received a 401 Unauthorized '
                 'response invoking method "metric_data" with payload "[\\"123456789\\",[]]"'))
main.append(line('2026-08-18 14:04:00,010', 'ERROR', 8412, 5,
                 'Request(33333333-3333-3333-3333-333333333333): An error occurred invoking '
                 'method "metric_data" with payload "[]": System.Net.WebException: The remote '
                 'server returned an error while calling '
                 'https://collector-1.nr.com/agent_listener/invoke_raw_method?method=metric_data'
                 '&license_key=' + KEY + '&marshal_format=json'))
main.append('   at NewRelic.Agent.Core.DataTransport.HttpCollectorWire.SendData()\n')
main.append('   at NewRelic.Agent.Core.DataTransport.DataTransportService.Send()\n')
main.append(line('2026-08-18 14:05:00,000', 'ERROR', 8412, 5,
                 'Request(44444444-4444-4444-4444-444444444444): Dropped large payload: '
                 'size: 2000000, max_payload_size_bytes=1000000'))
main.append(line('2026-08-18 14:06:00,000', 'FINEST', 8412, 7,
                 'No transaction, skipping method MyCo.Worker.Poll(System.String)'))
main.append(line('2026-08-18 14:31:02,881', 'INFO', 8412, 1,
                 'The New Relic .NET Agent v10.44.0 has shutdown (pid 8412) on app domain '
                 "'/LM/W3SVC/2/ROOT'"))

with open(os.path.join(OUT, 'newrelic_agent_MyApp.log'), 'w') as f:
    f.writelines(main)

# multi-app-domain session: one pid, three start banners, one runtime level change
shared = []
for index, domain in enumerate(('/', '/DataServices', '/AngularClient')):
    shared.append(line('2026-08-18 16:00:0%d,000' % index, 'INFO', 6000, 1,
                       'The New Relic .NET Agent v10.53.0.0 started (pid 6000) on app domain '
                       "'%s'" % domain))
    shared.append(line('2026-08-18 16:00:0%d,500' % index, 'INFO', 6000, 1,
                       'Log level set to INFO'))
shared.append(line('2026-08-18 16:10:00,000', 'INFO', 6000, 1,
                   'The log level was updated to FINEST from INFO'))
shared.append(line('2026-08-18 16:10:00,100', 'FINEST', 6000, 9,
                   'Trx Noop: Attempting to execute wrapper'))

with open(os.path.join(OUT, 'newrelic_agent_Shared.log'), 'w') as f:
    f.writelines(shared)

prof = [
    '[Info ] 2026-08-18 14:03:09 Logger initialized.\n',
    '[Info ] 2026-08-18 14:03:09 Found newrelic.config at: '
    'C:\\ProgramData\\New Relic\\.NET Agent\\newrelic.config\n',
    '[Info ] 2026-08-18 14:03:09 Loading instrumentation from '
    'C:\\ProgramData\\New Relic\\.NET Agent\\extensions\n',
    '[Error] 2026-08-18 14:03:09 An exception was thrown while reading instrumentation file: '
    'C:\\ProgramData\\New Relic\\.NET Agent\\extensions\\custom.xml - ignoring this file.\n',
    '[Info ] 2026-08-18 14:03:10 Profiler initialized\n',
    '[Info ] 2026-08-18 14:03:11 Unable to find MyCo.Missing.Class for rejit. HR:-2147483645\n',
    '[Info ] 2026-08-18 14:03:12 Instrumenting method: MyCo.Worker.Poll(System.String)\n',
    '[Info ] 2026-08-18 14:03:12 Instrumenting API method: '
    'NewRelic.Api.Agent.NewRelic.NoticeError(System.Exception)\n',
    '[Warn ] 2026-08-18 14:03:13 Unable to find the New Relic Agent extensions directory '
    '(C:\\nope).\n',
]
with open(os.path.join(OUT, 'NewRelic.Profiler.8412.log'), 'w') as f:
    f.writelines(prof)

rejected = [
    '[Info ] 2026-08-18 15:00:00 This process (C:\\Apps\\MyApp\\MyApp.exe) is not configured '
    'to be instrumented.\n',
    '[Info ] 2026-08-18 15:00:00 This process should not be instrumented, unloading profiler.\n',
]
with open(os.path.join(OUT, 'NewRelic.Profiler.7777.log'), 'w') as f:
    f.writelines(rejected)

print('fixtures written to %s' % OUT)
print('license key planted: %s' % KEY)
