#pragma once
#include <stdint.h>
#include <stdlib.h>

#ifdef DEBUG
#define GUARD_HEAP
#endif

#ifdef DEBUG
void memfail();
#define MEM_FAIL()    { memfail(); }
#define MEM_ASSERT(x) { if (!(x)) { memfail(); }}
#else
#define MEM_FAIL() {}
#define MEM_ASSERT(x) {}
#endif

#ifdef GUARD_HEAP

#define GUARD_PAD 32

static_assert(GUARD_PAD > sizeof(int64_t) * 2, "Guard pad needs to be bigger.");

struct GuardHeader {
    // Where the constant for the pad can be any 2^n greater than the size of 2 int64_t
    static const int PAD = GUARD_PAD - 2 * sizeof(int64_t);
    static const int HEAD_SENTINEL = 0xa1;
    static const int TAIL_SENTINEL = 0xb1;

    int64_t size;
    int64_t offset;
    uint8_t pad[PAD];
};

// Pointer to the memory that will be returned; setting up the padding
// is done before this call.
void setupGuardedMemory(void* mem, int headerSize, int64_t size);
void checkGuardedMemory(void* mem, bool poison);

#endif

