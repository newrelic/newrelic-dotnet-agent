#pragma once
#include "../Common/Macros.h"
#include "../Common/xplat.h"
#include "../Configuration/Configuration.h"
#include "../Configuration/InstrumentationConfiguration.h"
#include "../Logging/Logger.h"
#include "Exceptions.h"
#include "FunctionManipulator.h"
#include "IFunction.h"
#include "Instrumentors.h"
#include <iomanip>
#include <memory>
#include <stdint.h>
#include <string>
#include <regex>
#include <unordered_map>

#include "../Configuration/Strings.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter {

	class MethodRewriter {
	public:
		MethodRewriter(Configuration::ConfigurationPtr configuration, Configuration::InstrumentationConfigurationPtr instrumentationConfiguration, const ISystemCallsPtr& system)
			: _configuration(configuration)
			, _instrumentationConfiguration(instrumentationConfiguration)
			, _instrumentedAssemblies(new std::set<xstring_t>())
			, _instrumentedFunctionNames(new std::set<xstring_t>())
			, _instrumentedTypes(new std::set<xstring_t>())
			, _helperInstrumentor(std::make_unique<HelperInstrumentor>())
			, _apiInstrumentor(std::make_unique<ApiInstrumentor>())
			, _defaultInstrumentor(std::make_unique<DefaultInstrumentor>())
			, _corePath(GetCorePath(system))
		{
			Initialize();
		}

		MethodRewriter(std::shared_ptr<MethodRewriter> oldMethodRewriter, Configuration::InstrumentationConfigurationPtr instrumentationConfiguration, const ISystemCallsPtr& system)
			: MethodRewriter(oldMethodRewriter->_configuration, instrumentationConfiguration, system)
		{
		}

		void Initialize()
		{
			// We have to instrument mscorlib to add our hooks.  Yes, this is a little brittle
			// and it should probably live closer to the code that mucks with these methods.
			_instrumentedAssemblies->emplace(_X("mscorlib"));
			_instrumentedTypes->emplace(_X("System.CannotUnloadAppDomainException"));
			_instrumentedFunctionNames->emplace(_X("GetAppDomainBoolean"));
			_instrumentedFunctionNames->emplace(_X("GetThreadLocalBoolean"));
			_instrumentedFunctionNames->emplace(_X("SetThreadLocalBoolean"));
			_instrumentedFunctionNames->emplace(_X("GetMethodFromAppDomainStorageOrReflectionOrThrow"));
			_instrumentedFunctionNames->emplace(_X("GetMethodFromAppDomainStorage"));
			_instrumentedFunctionNames->emplace(_X("GetMethodViaReflectionOrThrow"));
			_instrumentedFunctionNames->emplace(_X("GetTypeViaReflectionOrThrow"));
			_instrumentedFunctionNames->emplace(_X("LoadAssemblyOrThrow"));
			_instrumentedFunctionNames->emplace(_X("StoreMethodInAppDomainStorageOrThrow"));

			for (auto instrumentationPoint : *_instrumentationConfiguration->GetInstrumentationPoints().get()) {
				_instrumentedAssemblies->emplace(instrumentationPoint->AssemblyName);
				_instrumentedFunctionNames->emplace(instrumentationPoint->MethodName);
				_instrumentedTypes->emplace(instrumentationPoint->ClassName);
			}
		}

		virtual ~MethodRewriter()
		{
		}

		Configuration::InstrumentationConfigurationPtr GetInstrumentationConfiguration()
		{
			return _instrumentationConfiguration;
		}

		xstring_t GetCorePath()
		{
			return _corePath;
		}

		std::set<Configuration::InstrumentationPointPtr> GetAssemblyInstrumentation(xstring_t assemblyName)
		{
			std::set<Configuration::InstrumentationPointPtr> set;
			for (auto instrumentationPoint : *_instrumentationConfiguration->GetInstrumentationPoints().get()) {
				if (assemblyName == instrumentationPoint->AssemblyName) {
					set.emplace(instrumentationPoint);
				}
			}
			return set;
		}

		bool ShouldNotInstrumentCommandNetCore(xstring_t const& commandLine)
		{
			//If it contains MsBuild, it is a build command and should not be profiled.
			bool shouldNotInstrument = Strings::ContainsCaseInsensitive(commandLine, _X("MSBuild.dll"));

			//Search for "dotnet run" or "dotnet publish" variations using a regular expression.
			//If it is a hit, it should not instrument the invocation of dotnet.exe
			//Example Hits:	dotnet run
			//				dotnet.exe run -f netcoreapp2.2
			//				"c\program files\dotnet.exe" run
			//				f:\program files\dotnet.exe run -f netcoreapp2.2
			//				all of the above with publish instead of run.

			auto needle = std::wregex(L".*(dotnet)(\\.exe)?(\")?\\s+(run|publish|restore|new)(\\s+.*|$)", std::regex_constants::icase);

			std::wstring haystack = std::wstring(commandLine.begin(), commandLine.end());
			shouldNotInstrument = shouldNotInstrument || std::regex_search(haystack, needle);

			return shouldNotInstrument;
		}

		// test to see if we should instrument this application at all
		bool ShouldInstrumentNetFramework(xstring_t const& processName, xstring_t const& appPoolId)
		{
			LogTrace("Checking to see if we should instrument this process.");
			return _configuration->ShouldInstrumentProcess(processName, appPoolId);
		}

		bool ShouldInstrumentAssembly(xstring_t assemblyName)
		{
			return InSet(_instrumentedAssemblies, assemblyName);
		}

		bool ShouldInstrumentType(xstring_t typeName)
		{
			return InSet(_instrumentedTypes, typeName);
		}

		bool ShouldInstrumentFunction(xstring_t functionName)
		{
			return InSet(_instrumentedFunctionNames, functionName);
		}

		// instrument the provided method (if necessary)
		void Instrument(IFunctionPtr function)
		{
			LogTrace("Possibly instrumenting: ", function->ToString());

			InstrumentationSettingsPtr instrumentationSettings = std::make_shared<InstrumentationSettings>(_instrumentationConfiguration, _corePath);

			if (_helperInstrumentor->Instrument(function, instrumentationSettings) || _apiInstrumentor->Instrument(function, instrumentationSettings) || _defaultInstrumentor->Instrument(function, instrumentationSettings)) {
			}
		}

	private:
		xstring_t _corePath;
		Configuration::ConfigurationPtr _configuration;
		Configuration::InstrumentationConfigurationPtr _instrumentationConfiguration;
		std::shared_ptr<std::set<xstring_t>> _instrumentedAssemblies;
		std::shared_ptr<std::set<xstring_t>> _instrumentedTypes;
		std::shared_ptr<std::set<xstring_t>> _instrumentedFunctionNames;

		std::unique_ptr<HelperInstrumentor> _helperInstrumentor;
		std::unique_ptr<ApiInstrumentor> _apiInstrumentor;
		std::unique_ptr<DefaultInstrumentor> _defaultInstrumentor;

		static bool InSet(std::shared_ptr<std::set<xstring_t>> set, xstring_t value)
		{
			return set.get()->find(value) != set.get()->end();
		}

		static xstring_t GetCorePath(const ISystemCallsPtr& system)
		{
			//GetEnvironmentVariable(NEWRELIC_INSTALL_PATH) ? ? GetEnvironmentVariable(NEWRELIC_HOME)
			auto maybeCorePath = TryGetCorePathFromBasePath(system, system->GetNewRelicInstallPath());
			if (maybeCorePath != nullptr)
				return *maybeCorePath;

			maybeCorePath = TryGetCorePathFromBasePath(system, system->GetNewRelicHomePath());
			if (maybeCorePath != nullptr)
				return *maybeCorePath;

			LogError(L"Unable to find ", system->GetNewRelicInstallPath(), L" or ", system->GetNewRelicHomePath(), L" environment variables.  Aborting instrumentation.");
			// FIXME
			throw MethodRewriterException(_X("Unable to find NEWRELIC_INSTALL_PATH or NEWRELIC_HOME environment variables."));
		}

		static std::unique_ptr<xstring_t> TryGetCorePathFromBasePath(const ISystemCallsPtr& system, const xstring_t& basePath)
		{
			auto installPath = system->TryGetEnvironmentVariable(basePath);
			if (installPath == nullptr)
				return nullptr;

			if (system->FileExists(*installPath.get())) {
				return installPath;
			}

			return std::unique_ptr<xstring_t>(new xstring_t(*installPath + PATH_SEPARATOR + _X("NewRelic.Agent.Core.dll")));
		}
	};
	typedef std::shared_ptr<MethodRewriter> MethodRewriterPtr;

}}}
