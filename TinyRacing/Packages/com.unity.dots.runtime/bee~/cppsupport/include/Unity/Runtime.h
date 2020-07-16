#pragma once
#include <cstdint>

#if WIN32

#define DOTS_EXPORT(rtype) extern "C" __declspec(dllexport) rtype __stdcall
#define DOTS_IMPORT(rtype) extern "C" __declspec(dllimport) rtype __stdcall
#define DOTS_CPP_EXPORT __declspec(dllexport)
#define DOTS_CPP_IMPORT __declspec(dllimport)

#else

#define DOTS_EXPORT(rtype) extern "C" __attribute__ ((visibility ("default"), used)) rtype
#define DOTS_IMPORT(rtype) extern "C" rtype
#define DOTS_CPP_EXPORT __attribute__ ((visibility ("default")))
#define DOTS_CPP_IMPORT

#endif
