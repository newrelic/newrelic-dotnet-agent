/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <memory>
#include <mutex>
#include <fstream> //wofstream
#include <iostream>
#include <ostream>
#include <atomic>
#include <cassert>
#include "../Common/xplat.h"

#ifdef PAL_STDCPP_COMPAT
// This makes the logging calls work on unix systems by converting 2 byte wide strings into
// 4 byte wide strings (convert char16_t chars to wchar_t.)
std::basic_ostream<wchar_t, std::char_traits<wchar_t>>& operator<<(std::basic_ostream<wchar_t, std::char_traits<wchar_t>>& _Ostr, const xstring_t& str)
{
    std::copy(str.cbegin(), str.cend(), std::ostream_iterator<wchar_t, wchar_t>(_Ostr));
    return _Ostr;
}
#endif

namespace NewRelic { namespace Profiler { namespace Logger { } } };
namespace nrlog = NewRelic::Profiler::Logger;

namespace NewRelic {
    namespace Profiler {
        namespace Logger {

            //track whether or not the logger is constructed and in good standing.  Checked by the 
            //Logging macros to bail on a log call if the logger has been destroyed.  This is defensive
            //coding. This should not occur. This means a thread is running (not joined) during CRT
            //tear down.
            extern volatile bool logging_available;

            enum class Level { LEVEL_TRACE, LEVEL_DEBUG, LEVEL_INFO, LEVEL_WARN, LEVEL_ERROR };
            extern wchar_t const* GetLevelString(Level level);

            template <typename _Ostr>
            class Logger 
            {
            public:
                using char_type = typename _Ostr::char_type;
                using _Mystreamtype = std::basic_ostream<char_type>;
                using _Mymut = std::mutex;
                using _Mylockgrd = std::lock_guard<_Mymut>;
                Logger(_Ostr&& myostr, Level level) : _level(level), _destination(std::move(myostr)), _console(false), _enabled(true), _initialized(false)
                {
                    logging_available = true;
                }

                //No copy ctor, move ctor, copy assignment, or move assignment
                Logger(const Logger&) = delete;
                Logger(Logger&&) = delete;
                Logger& operator=(const Logger&) = delete;
                Logger& operator=(Logger&&) = delete;
                virtual ~Logger()
                {
                    logging_available = false;
                    _Mylockgrd lock(_mutex);
                }

                //void SetDestination(_Ostr&& newDestination)
                //{
                //    //move to prevent copy and reference count modifications already done creating argument newDestination.
                //    LoggerBase::_Mylockgrd lock(_mutex);
                //    _destination = std::move(newDestination);
                //}
                _Ostr& get_dest() noexcept
                {
                    return _destination;
                }

                _Mystreamtype& ostr() noexcept
                {
                    return _destination;
                }

                void SetLevel(Level newLevel) noexcept
                {
                    _level = newLevel;
                }

                Level GetLevel() const noexcept
                {
                    if (!_console)
                    {
                        return _level;
                    }
                    // Console logging at debug or trace level incurs a very large
                    // performance hit. Clamp the log level to INFO in that case.
                    return (_level < Level::LEVEL_INFO) ? Level::LEVEL_INFO : _level;
                }

                void SetConsoleLogging(bool enabled)
                {
                    _console = enabled;
                }

                bool GetConsoleLogging()
                {
                    return _console;
                }
                void SetEnabled(bool enabled)
                {
                    _enabled = enabled;
                }
                bool GetEnabled()
                {
                    return _enabled;
                }
                void SetInitalized()
                {
                    _initialized = true;
                }
                bool GetInitialized()
                {
                    return _initialized;
                }

                _Mymut& mutex() const noexcept
                {
                    return _mutex;
                }
            private:
                _Ostr _destination;
                Level _level;
                mutable _Mymut _mutex;
                bool _console;
                bool _enabled;
                bool _initialized;
            };

            using FileLogger = Logger<std::wofstream>;
            using MemoryLogger = Logger<std::wostringstream>;

            // "%Y-%m-%d %X" using the native chars of the stream in the log
            template <typename _Elem>
            struct format_traits {
                static const _Elem str[sizeof("%Y-%m-%d %X")];
            };

            template <typename _Elem>
            const _Elem format_traits<_Elem>::str[] = {
                _Elem('%'), _Elem('Y'), _Elem('-'),
                _Elem('%'), _Elem('m'), _Elem('-'),
                _Elem('%'), _Elem('d'), _Elem(' '),
                _Elem('%'), _Elem('X'), _Elem('\0')
            };

            // Visit http://en.cppreference.com/w/cpp/chrono/c/strftime for more information about date/time format
            template <typename _Log, class... _Args>
            void LogStuff(_Log& log, Level level, _Args&&... args) noexcept
            {
                //Logging macros to bail on a log call if the logger has been destroyed.  This is defensive
                //coding. This should not occur. This means a thread is running (not joined) during CRT
                //tear down.
                if (!logging_available)
                {
                    assert(!"attempting to log after logger destroyed.");
                    return;
                }

                using stream_char_t = typename _Log::char_type;
                if (log.GetInitialized() && log.GetEnabled() && (level >= log.GetLevel()))
                {
                    //each thread will have these on the stack...
                    std::tm  tstruct;
                    std::time_t now;
                    (void)time(&now);
                    (void)gmtime_s(&tstruct, &now);
                    auto levelstr = nrlog::GetLevelString(level);
                    std::wostream& strm = log.GetConsoleLogging() ? std::wcout : log.ostr();

                    try
                    {
                        //acquire a lock to serialize access to the log's stream.
                        typename _Log::_Mylockgrd lock(log.mutex());
                        strm << stream_char_t('[') << levelstr << "] " << std::put_time<stream_char_t>(&tstruct, format_traits<stream_char_t>::str) << stream_char_t(' ');

                        using expander = int[];
                        (void)expander {
                            0, (void(strm << std::forward<_Args>(args)), 0)...
                        };
                        strm << std::endl; //new line and flush versus << '\n'
                    }
                    catch (...) 
                    {
                        //avoid exception possibility of calling clear with no rdbuf().
                        if (strm.rdbuf()) strm.clear();
                    }
                }
            }

            template <typename _LogT>
            struct LogScope
            {
                LogScope(_LogT& log, Level level, const char* info)
                    : _log(log), _level(level), _info(info)
                {
                    nrlog::LogStuff(_log, _level, L"Enter: ", _info);
                }
                ~LogScope()
                {
                    nrlog::LogStuff(_log, _level, L"Leave: ", _info);
                }
                _LogT& _log;
                Level _level;
                const char* _info;
            };
        }
    }
}


#define _NRLOG_STRINGIZE_(x) #x
#define _NRLOG_STRINGIZE(x) _NRLOG_STRINGIZE_(x)

#define LogScopeEnterLeaveTo(log, level) nrlog::LogScope<decltype(log)> scopelogger_ ## __COUNTER__ (log, level, __FUNCTION__ " on " __FILE__ "("  _NRLOG_STRINGIZE(__LINE__) ")") 
#define LogScopeEnterLeave(level) LogScopeEnterLeaveTo(nrlog::StdLog, level) 


#define LogTraceTo(log, ...) nrlog::LogStuff(log, nrlog::Level::LEVEL_TRACE, __VA_ARGS__)
#define LogDebugTo(log, ...) nrlog::LogStuff(log, nrlog::Level::LEVEL_DEBUG, __VA_ARGS__)
#define LogInfoTo(log, ...) nrlog::LogStuff(log, nrlog::Level::LEVEL_INFO, __VA_ARGS__)
#define LogWarnTo(log, ...) nrlog::LogStuff(log, nrlog::Level::LEVEL_WARN, __VA_ARGS__)
#define LogErrorTo(log, ...) nrlog::LogStuff(log, nrlog::Level::LEVEL_ERROR, __VA_ARGS__)

#define LogTrace(...) LogTraceTo(nrlog::StdLog, __VA_ARGS__)
#define LogDebug(...) LogDebugTo(nrlog::StdLog, __VA_ARGS__)
#define  LogInfo(...)  LogInfoTo(nrlog::StdLog, __VA_ARGS__)
#define  LogWarn(...)  LogWarnTo(nrlog::StdLog, __VA_ARGS__)
#define LogError(...) LogErrorTo(nrlog::StdLog, __VA_ARGS__)

namespace NewRelic {
    namespace Profiler {
        namespace Logger {
#ifdef LOGGER_STDLOG_USE_MEMORYLOGGER
            extern MemoryLogger StdLog;
#else
            extern FileLogger StdLog;
#endif
        }
    }
}


//define in one compilation unit per binary executable (ODR)
#ifdef LOGGER_DEFINE_STDLOG
namespace NewRelic {
    namespace Profiler {
        namespace Logger {
            volatile bool logging_available{ false };

#ifdef LOGGER_STDLOG_USE_MEMORYLOGGER
            MemoryLogger StdLog(std::wostringstream(), Level::LEVEL_TRACE);
#else
            FileLogger StdLog(std::wofstream(), Level::LEVEL_TRACE);
#endif

            wchar_t const* GetLevelString(Level level)
            {
                wchar_t const *LevelStrings[] = { L"Trace", L"Debug", L"Info ", L"Warn ", L"Error" };
                switch (level) {
                case Level::LEVEL_TRACE: return LevelStrings[0];
                case Level::LEVEL_DEBUG: return LevelStrings[1];
                case Level::LEVEL_INFO: return LevelStrings[2];
                case Level::LEVEL_WARN: return LevelStrings[3];
                case Level::LEVEL_ERROR: return LevelStrings[4];
                default: return L"-bad level-";
                }
            }

        }
    }
}
#endif
