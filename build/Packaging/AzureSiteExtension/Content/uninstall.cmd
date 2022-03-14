:: Copyright 2020 New Relic Corporation. All rights reserved.
:: SPDX-License-Identifier: Apache-2.0

SET NEW_RELIC_FOLDER="%HOME%\NewRelicAgent\newrelic"
IF EXIST %NEW_RELIC_FOLDER% (
  rd /S /q %NEW_RELIC_FOLDER%
)

SET NEW_RELIC_FOLDER="%HOME%\NewRelicAgent\newrelic_core"
IF EXIST %NEW_RELIC_FOLDER% (
  rd /S /q %NEW_RELIC_FOLDER%
)