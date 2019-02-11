#pragma once

#include "FunctionManipulator.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
	// This thing writes the helper methods that we add to
	// System.Type System.CannotUnloadAppDomainException
	class HelperFunctionManipulator : public FunctionManipulator
	{
	public:
		HelperFunctionManipulator(IFunctionPtr function) : 
			FunctionManipulator(function)
		{ }

	protected:
		// instrument this method with the mscorlib helper stuff
		virtual bool DoWriteFunction() override
		{
			if (_function->GetFunctionName() == _X("LoadAssemblyOrThrow"))
			{
				BuildLoadAssemblyOrThrow();
			}
			else if (_function->GetFunctionName() == _X("GetTypeViaReflectionOrThrow"))
			{
				BuildGetTypeViaReflectionOrThrow();
			}
			else if (_function->GetFunctionName() == _X("GetMethodViaReflectionOrThrow"))
			{
				BuildGetMethodViaReflectionOrThrow();
			}
			else if (_function->GetFunctionName() == _X("StoreMethodInAppDomainStorageOrThrow"))
			{
				BuildStoreMethodInAppDomainStorageOrThrow();
			}
			else if (_function->GetFunctionName() == _X("GetMethodFromAppDomainStorage"))
			{
				BuildGetMethodFromAppDomainStorage();
			}
			else if (_function->GetFunctionName() == _X("GetMethodFromAppDomainStorageOrReflectionOrThrow"))
			{
				BuildGetMethodFromAppDomainStorageOrReflectionOrThrow();
			}
			else
			{
				LogError(L"Attempted to instrument an unknown helper method in mscorlib.");
				return false;
			}
			InstrumentTiny();
			return true;
		}
	private:

		// System.Reflection.Assembly LoadAssemblyOrThrow(String assemblyPath)
		void BuildLoadAssemblyOrThrow()
		{
			_instructions->Append(CEE_LDARG_0);
			_instructions->Append(CEE_CALL, _X("class System.Reflection.Assembly System.Reflection.Assembly::LoadFrom(string)"));
			ThrowExceptionIfStackItemIsNull(_instructions, _X("Failed to load assembly."), true);
			_instructions->Append(CEE_RET);
		}

		// System.Type GetTypeViaReflectionOrThrowWithoutTypeParameters(String assemblyPath, String typeName)
		void BuildGetTypeViaReflectionOrThrow()
		{
			_instructions->Append(CEE_LDARG_0);
			_instructions->Append(CEE_CALL, _X("class System.Reflection.Assembly System.CannotUnloadAppDomainException::LoadAssemblyOrThrow(string)"));
			_instructions->Append(CEE_LDARG_1);
			_instructions->Append(CEE_CALLVIRT, _X("instance class System.Type System.Reflection.Assembly::GetType(string)"));
			ThrowExceptionIfStackItemIsNull(_instructions, _X("Failed to load type from assembly via reflection."), true);
			_instructions->Append(CEE_RET);
		}

		// System.Reflection.MethodInfo GetMethodViaReflectionOrThrow(String assemblyName, String typeName, String methodName, System.Type[] methodParameterTypes)
		void BuildGetMethodViaReflectionOrThrow()
		{
			_instructions->Append(CEE_LDARG_0);
			_instructions->Append(CEE_LDARG_1);
			_instructions->Append(CEE_CALL, _X("class System.Type System.CannotUnloadAppDomainException::GetTypeViaReflectionOrThrow(string,string)"));
			_instructions->Append(CEE_LDARG_2);
			_instructions->Append(CEE_LDARG_3);

			// if (methodParameterTypes != null)
			auto typeArrayIsNullLabel = _instructions->AppendJump(CEE_BRFALSE);
			{
				_instructions->Append(CEE_LDARG_3);
				_instructions->Append(CEE_CALLVIRT, _X("instance class System.Reflection.MethodInfo System.Type::GetMethod(string,class System.Type[])"));
				_instructions->AppendJump(CEE_BR, _X("after_GetMethod"));
			}
			// else
			_instructions->AppendLabel(typeArrayIsNullLabel);
			{
				_instructions->Append(CEE_CALLVIRT, _X("instance class System.Reflection.MethodInfo System.Type::GetMethod(string)"));
				_instructions->AppendJump(CEE_BR, _X("after_GetMethod"));
			}
			_instructions->AppendLabel(_X("after_GetMethod"));

			ThrowExceptionIfStackItemIsNull(_instructions, _X("Failed to load method from type via reflection."), true);
			_instructions->Append(CEE_RET);
		}

		// We can't directly inject calls to AppDomain.GetData and SetData in many cases
		// because of security permissions.  The methods added onto an mscorlib exception
		// work around this.
		//
		// void StoreMethodInAppDomainStorageOrThrow(MethodInfo method, String storageKey)
		void BuildStoreMethodInAppDomainStorageOrThrow()
		{
			_instructions->Append(CEE_CALL, _X("class System.AppDomain System.AppDomain::get_CurrentDomain()"));
			ThrowExceptionIfStackItemIsNull(_instructions, _X("System.AppDomain.CurrentDomain == null."), true);
			_instructions->Append(CEE_LDARG_1);
			_instructions->Append(CEE_LDARG_0);
			_instructions->Append(CEE_CALLVIRT, _X("instance void System.AppDomain::SetData(string, object)"));
			_instructions->Append(CEE_RET);
		}

		// MethodInfo GetMethodFromAppDomainStorage(String storageKey)
		void BuildGetMethodFromAppDomainStorage()
		{
			_instructions->Append(CEE_CALL, _X("class System.AppDomain System.AppDomain::get_CurrentDomain()"));
			ThrowExceptionIfStackItemIsNull(_instructions, _X("System.AppDomain.CurrentDomain == null."), true);
			_instructions->Append(CEE_LDARG_0);
			_instructions->Append(CEE_CALLVIRT, _X("instance object System.AppDomain::GetData(string)"));
			_instructions->Append(CEE_CASTCLASS, _X("class System.Reflection.MethodInfo"));
			_instructions->Append(CEE_RET);
		}

		// MethodInfo GetMethodFromAppDomainStorageOrReflectionOrThrow(String storageKey, String assemblyPath, String typeName, String methodName, Type[] methodParameters)
		void BuildGetMethodFromAppDomainStorageOrReflectionOrThrow()
		{
			_instructions->Append(CEE_LDARG_0);
			_instructions->Append(CEE_CALL, _X("class System.Reflection.MethodInfo System.CannotUnloadAppDomainException::GetMethodFromAppDomainStorage(string)"));

			_instructions->Append(CEE_DUP);
			auto methodEnd = _instructions->AppendJump(CEE_BRTRUE);

			_instructions->Append(CEE_POP);
			_instructions->Append(CEE_LDARG_1);
			_instructions->Append(CEE_LDARG_2);
			_instructions->Append(CEE_LDARG_3);
			_instructions->Append(CEE_LDARG_S, (uint8_t)4);
			_instructions->Append(CEE_CALL, _X("class System.Reflection.MethodInfo System.CannotUnloadAppDomainException::GetMethodViaReflectionOrThrow(string,string,string,class System.Type[])"));
			_instructions->Append(CEE_DUP);
			_instructions->Append(CEE_LDARG_0);
			_instructions->Append(CEE_CALL, _X("void System.CannotUnloadAppDomainException::StoreMethodInAppDomainStorageOrThrow(class System.Reflection.MethodInfo,string)"));

			_instructions->AppendLabel(methodEnd);
			_instructions->Append(CEE_RET);
		}
	};
}}}