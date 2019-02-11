#pragma once
#include <vector>
#include <array>
#include <iterator>
#include <memory>
#include <cor.h>
#include <corprof.h>
#include "../Common/xplat.h"

namespace NewRelic {
	namespace Profiler {
		namespace ThreadProfiler
		{
			static constexpr std::size_t MAX_TYPE_NAME_LENGTH = 1023;
			static constexpr std::size_t MAX_METHOD_NAME_LENGTH = 1023;
			using PreallocTypeName = std::pair<std::array<xchar_t, MAX_TYPE_NAME_LENGTH>, ULONG>;
			using PreallocMethodName = std::pair<std::array<xchar_t, MAX_METHOD_NAME_LENGTH>, ULONG>;

			//holds a reference to the type name that is in _typeDefNameMap and the actual string for the method name
			class TypeAndMethodNames
			{
			public:
				TypeAndMethodNames & operator=(const TypeAndMethodNames& other) = delete;

				TypeAndMethodNames(std::shared_ptr<xstring_t> typeName, xstring_t methodName) noexcept : _typeName(std::move(typeName)), _methodName(std::move(methodName))
				{}

				static std::shared_ptr<xstring_t> GetUnknownTypeName()
				{
					static const std::shared_ptr<xstring_t> UnknownTypeName = std::make_shared<xstring_t>(_X("UnknownClass"));
					return UnknownTypeName;
				}

				static const TypeAndMethodNames& GetUnknownTypeAndMethodNames()
				{
					static const TypeAndMethodNames UnknownTypeAndMethod{ GetUnknownTypeName(), _X("UnknownMethod(error)") };
					return UnknownTypeAndMethod;
				}

				const xchar_t * TypeName() const noexcept
				{
					return _typeName->c_str();
				}

				const xchar_t * MethodName() const noexcept
				{
					return _methodName.c_str();
				}
			private:
				std::shared_ptr<xstring_t> _typeName;
				const xstring_t _methodName;
			};

			class NameCache
			{
				//function names cache implementation
				using FidNameMap = std::vector < std::pair<FunctionID, TypeAndMethodNames>>;
				using TypedefNameMap = std::vector < std::pair<mdTypeDef, std::shared_ptr<xstring_t>>>;
			public:

				bool has_fid(FunctionID fid) const
				{
					return find_fid(fid) != std::cend(fidNameMap);
				}

				bool has_typedef(mdTypeDef typeDef) const
				{
					return find_typedef(typeDef) != std::cend(typedefNameMap);
				}

				const TypeAndMethodNames& operator[](FunctionID fid) const
				{
					const auto itr = find_fid(fid);
					return itr != std::cend(fidNameMap) ? itr->second : TypeAndMethodNames::GetUnknownTypeAndMethodNames();
				}

				const std::shared_ptr<xstring_t> typename_for(mdTypeDef typeDef) const
				{
					const auto itr = find_typedef(typeDef);
					return itr != std::cend(typedefNameMap) ? itr->second : TypeAndMethodNames::GetUnknownTypeName();
				}

				void clear() noexcept
				{
					fidNameMap.clear();
					typedefNameMap.clear();
				}

				void insert(FunctionID functionId, mdTypeDef typeDef, const PreallocTypeName& typeName, const PreallocMethodName& methodName)
				{
					//PreallocTypeName/PreallocMethodName  .second is the actual length of the strings INCLUDING THE NULL terminator.  
					//   .second-1 to exclude the null from the xstring_t
					auto itr = find_typedef(typeDef);
					if (std::cend(typedefNameMap) == itr)
					{
						itr = typedefNameMap.emplace(std::end(typedefNameMap), std::piecewise_construct, std::forward_as_tuple(typeDef), std::forward_as_tuple(std::make_shared<xstring_t>(typeName.first.data(), typeName.second - 1)));
					}
					fidNameMap.emplace_back(std::piecewise_construct, std::forward_as_tuple(functionId), std::forward_as_tuple(itr->second, xstring_t(methodName.first.data(), methodName.second - 1)));
				}

			private:

				FidNameMap::const_iterator find_fid(FunctionID fid) const
				{
					return std::find_if(std::cbegin(fidNameMap), std::cend(fidNameMap),
						[=](const FidNameMap::value_type& pr) noexcept { return pr.first == fid; });
				}

				TypedefNameMap::const_iterator find_typedef(mdTypeDef typeDef) const
				{
					return std::find_if(std::cbegin(typedefNameMap), std::cend(typedefNameMap),
						[=](const TypedefNameMap::value_type& pr) noexcept { return pr.first == typeDef; });
				}

				FidNameMap fidNameMap;
				TypedefNameMap typedefNameMap;
			};
		} // namespace ThreadProfiler
	} // namespace Profiler
} // namespace NewRelic
