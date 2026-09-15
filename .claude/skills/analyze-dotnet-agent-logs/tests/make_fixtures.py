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
BASE = os.path.dirname(os.path.abspath(OUT))
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
# pid 8412 line landing inside the 100 ms the pid 9100 session spans, so the pid
# clause of the session filter is covered on its own
main.append(line('2026-08-18 14:03:14,050', 'ERROR', 8412, 5,
                 'Unable to connect to the New Relic service at '
                 'https://collector-inside.nr.com:443'))
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
# continuation line carrying a signature, owned by pid 8412: it must follow its
# owner's session, not the session of whichever pid is being matched
main.append('   ---> Check your proxy settings (http://proxy.corp:8080)\n')
main.append(line('2026-08-18 14:05:00,000', 'ERROR', 8412, 5,
                 'Request(44444444-4444-4444-4444-444444444444): Dropped large payload: '
                 'size: 2000000, max_payload_size_bytes=1000000'))
main.append(line('2026-08-18 14:06:00,000', 'FINEST', 8412, 7,
                 'No transaction, skipping method MyCo.Worker.Poll(System.String)'))
main.append(line('2026-08-18 14:31:02,881', 'INFO', 8412, 1,
                 'The New Relic .NET Agent v10.44.0 has shutdown (pid 8412) on app domain '
                 "'/LM/W3SVC/2/ROOT'"))
# pid 8412 restarts an hour later in the same file: a second session whose lines
# share the pid but fall outside the earlier session's range, so the time clause
# of the session filter is covered on its own
main.append(line('2026-08-18 15:30:00,000', 'INFO', 8412, 1,
                 'The New Relic .NET Agent v10.44.0 started (pid 8412) on app domain '
                 "'/LM/W3SVC/2/ROOT'"))
main.append(line('2026-08-18 15:30:00,100', 'INFO', 8412, 1, 'Log level set to INFO'))
main.append(line('2026-08-18 15:30:05,000', 'ERROR', 8412, 5,
                 'Unable to connect to the New Relic service at '
                 'https://collector-later.nr.com:443'))
main.append(line('2026-08-18 15:35:00,000', 'INFO', 8412, 1,
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

# INFO-only session for pid 5150: no FINEST line, so a finest playbook must
# report blocked rather than clear. The 401 line carries the planted key inside
# text a playbook signature matches, so the redaction check has real evidence.
info_only = [
    line('2026-08-19 09:00:00,000', 'INFO', 5150, 1,
         "The New Relic .NET Agent v10.40.1 started (pid 5150) on app domain '/Quiet'"),
    line('2026-08-19 09:00:00,010', 'INFO', 5150, 1, 'Log level set to INFO'),
    line('2026-08-19 09:00:05,000', 'INFO', 5150, 1,
         'Agent fully connected. Application name(s): Quiet'),
    line('2026-08-19 09:00:06,000', 'ERROR', 5150, 5,
         'Received a 401 Unauthorized response invoking method "connect" with payload '
         '"https://collector-1.nr.com/agent_listener/invoke_raw_method?method=connect'
         '&license_key=' + KEY + '&marshal_format=json"'),
    line('2026-08-19 09:10:00,000', 'INFO', 5150, 1,
         "The New Relic .NET Agent v10.40.1 has shutdown (pid 5150) on app domain '/Quiet'"),
]
with open(os.path.join(OUT, 'newrelic_agent_Quiet.log'), 'w') as f:
    f.writelines(info_only)

# level set in newrelic.config, not by environment variable: the agent writes the
# level-change line during startup, before the banner, because the first
# configuration update compares against the built-in default of info. Stated and
# observed agree, so a runaway-volume verdict here would be a false positive.
config_debug = [
    line('2026-08-20 08:00:00,000', 'INFO', 7001, 1,
         'The log level was updated to DEBUG from INFO'),
    line('2026-08-20 08:00:00,010', 'INFO', 7001, 1, 'Log level set to DEBUG'),
    line('2026-08-20 08:00:00,020', 'INFO', 7001, 1,
         "The New Relic .NET Agent v10.44.0 started (pid 7001) on app domain '/Quiet2'"),
    line('2026-08-20 08:00:01,000', 'DEBUG', 7001, 1,
         'Environment Variable NEW_RELIC_APP_NAME value: ConfigDebug'),
    # the agent states the runtime at DEBUG only, so environment.txt can prove both
    # the parsed form here and the "not stated" form on the INFO-only Quiet session
    line('2026-08-20 08:00:01,100', 'DEBUG', 7001, 1,
         '.NET Runtime Version: .NET 8.0.10'),
    line('2026-08-20 08:05:00,000', 'INFO', 7001, 1,
         "The New Relic .NET Agent v10.44.0 has shutdown (pid 7001) on app domain '/Quiet2'"),
]
with open(os.path.join(OUT, 'newrelic_agent_ConfigDebug.log'), 'w') as f:
    f.writelines(config_debug)

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

# pid 5150 reused: the pid matches the Quiet session, the UTC range does not.
# Correlation must reject this file on time, not pair it on pid alone.
reused_pid = [
    '[Info ] 2026-08-19 07:00:00 Logger initialized.\n',
    '[Info ] 2026-08-19 07:00:01 Found newrelic.config at: '
    'C:\\ProgramData\\New Relic\\.NET Agent\\newrelic.config\n',
    '[Info ] 2026-08-19 07:00:05 Profiler initialized\n',
]
with open(os.path.join(OUT, 'NewRelic.Profiler.5150.log'), 'w') as f:
    f.writelines(reused_pid)

# playbook 10 (wrapper hard-disabled) plus playbook 8's FINEST signature, so the
# outranking test has a session where both match. No "Log level set to" line, so
# playbook 9 stays blocked rather than clear or matched.
wrapper_disabled = [
    line('2026-08-22 11:00:00,000', 'INFO', 6600, 1,
         "The New Relic .NET Agent v10.54.0 started (pid 6600) on app domain '/Orders'"),
    line('2026-08-22 11:00:05,000', 'ERROR', 6600, 5,
         'Wrapper SqlCommandWrapper is being disabled for '
         'System.Data.SqlClient.SqlCommand.ExecuteReader due to too many consecutive '
         'exceptions. All other methods using this wrapper will continue to be '
         'instrumented. This will reduce the functionality of the agent until the '
         'agent is restarted.'),
    line('2026-08-22 11:00:06,000', 'FINEST', 6600, 7,
         'No transaction, skipping method MyCo.Orders.Poll(System.String)'),
    line('2026-08-22 11:10:00,000', 'INFO', 6600, 1,
         "The New Relic .NET Agent v10.54.0 has shutdown (pid 6600) on app domain "
         "'/Orders'"),
]
with open(os.path.join(OUT, 'newrelic_agent_WrapperDisabled.log'), 'w') as f:
    f.writelines(wrapper_disabled)

# two applications side by side: triage must refuse to choose between them and
# ask for --file, because picking one customer application is a human decision
MULTI = os.path.join(BASE, 'multi')
os.makedirs(MULTI, exist_ok=True)
for app_pid, app in ((3101, 'AppOne'), (3102, 'AppTwo')):
    app_lines = [
        line('2026-08-21 10:00:00,000', 'INFO', app_pid, 1,
             'The New Relic .NET Agent v10.44.0 started (pid %d) on app domain '
             "'/%s'" % (app_pid, app)),
        line('2026-08-21 10:00:00,010', 'INFO', app_pid, 1, 'Log level set to INFO'),
        line('2026-08-21 10:00:05,000', 'INFO', app_pid, 1,
             'Agent fully connected. Application name(s): %s' % app),
        line('2026-08-21 10:05:00,000', 'INFO', app_pid, 1,
             'The New Relic .NET Agent v10.44.0 has shutdown (pid %d) on app domain '
             "'/%s'" % (app_pid, app)),
    ]
    with open(os.path.join(MULTI, 'newrelic_agent_%s.log' % app), 'w') as f:
        f.writelines(app_lines)

print('fixtures written to %s' % OUT)
print('multi-application fixtures written to %s' % MULTI)
print('license key planted: %s' % KEY)
