#pragma once

#include <Baselib.h>
#include <Cpp/Lock.h>

class BaselibLock {
public:
    BaselibLock() = delete;
    BaselibLock(baselib::Lock& mutex) : pMutex(&mutex) { mutex.Acquire(); }
    ~BaselibLock() { pMutex->Release(); }

private:
    baselib::Lock* pMutex;
};
