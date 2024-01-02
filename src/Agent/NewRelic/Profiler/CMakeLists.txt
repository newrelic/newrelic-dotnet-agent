cmake_minimum_required(VERSION 3.8)
project(NewRelicProfiler)
enable_language(CXX)
SET(CMAKE_BUILD_TYPE RelWithDebInfo)

if(${APPLE})
  set(DefaultBuildOS "OSX")
  add_definitions(-DPLATFORM_UNIX)
  add_definitions(-DPAL_STDCPP_COMPAT)
elseif(${WIN32})
  set(DefaultBuildOS "Windows")
else()
  set(DefaultBuildOS "Linux")
  add_definitions(-DPLATFORM_UNIX)
  add_definitions(-DPAL_STDCPP_COMPAT)
endif()
if(${CMAKE_SIZEOF_VOID_P} EQUAL "8")
  set(DefaultBuildArch "x64")
  add_definitions(-DBIT64)
else()
  set(DefaultBuildArch "x86")
endif()
set(CORECLR_PATH "~/runtime" CACHE PATH "Path to coreclr repository.")
set(BuildOS ${DefaultBuildOS} CACHE STRING "Operating system for build.")
set(BuildArch ${DefaultBuildArch} CACHE STRING "Build architecture (x64, x86)")
set(BuildType "Debug" CACHE STRING "Build type (Release, Debug)")
set(Output "NewRelicProfiler" CACHE STRING "Name of output file")
if(${WIN32})
  add_compile_options(
    -fPIC
    -fms-extensions
    -std=c++14
  )
else()
  set(THREADS_PREFER_PTHREAD_FLAG ON)
  find_package(Threads REQUIRED)
  set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -std=c++11 -fno-strict-aliasing -stdlib=libc++ -Wno-invalid-noreturn -Wno-ignored-attributes -Wno-macro-redefined -fms-extensions -fdeclspec -fPIC")
  set (CMAKE_SHARED_LINKER_FLAGS "-static-libstdc++") 
endif()

file(GLOB SOURCES
  "${CORECLR_PATH}/src/coreclr/inc/corhlpr.cpp"
  "Common/*.cpp"
  "Logging/*.cpp"
  "MethodRewriter/*.cpp"
  "ProfiledMethods/*.cpp"
  "Profiler/*.cpp"
  "RapidXML/*.cpp"
  "SignatureParser/*.cpp"
  "ThreadProfiler/*.cpp"
  "Sicily/ast/*.cpp"
  "Sicily/*.cpp"
)

add_library(${Output} SHARED ${SOURCES})

# We include files in two steps to force the order that the compiler chooses
include_directories(SYSTEM
  "${CORECLR_PATH}/src/coreclr/pal/inc/rt"
  "${CORECLR_PATH}/src/inc"
  "${CORECLR_PATH}/src/coreclr/inc"
)

include_directories(
  "${CORECLR_PATH}/src/coreclr/pal/prebuilt/inc"
  "${CORECLR_PATH}/src/coreclr/pal/inc"
)

target_link_libraries(${Output} ${LinkedLibs} Threads::Threads)
