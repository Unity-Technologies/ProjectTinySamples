#include "guard.h"

#include <stdlib.h>
#include <string.h>
#include <stdint.h>

#ifdef DEBUG
void memfail() {
#ifdef _WIN32
    __debugbreak();
#else
    abort();
#endif
}
#endif

#ifdef GUARD_HEAP

#define GUARD_HEAP_POISON 0xec  // the underlying allocators should poison on free; but be very sure.

static void guardcheck(uint8_t* p, uint8_t x, size_t s) {
    for ( size_t i=0; i<s; i++ ) {
        if (p[i]!=x) {
            memfail();
            return;
        }
    }
}

void setupGuardedMemory(void* mem, int headerSize, int64_t size)
{
    uint8_t* user = (uint8_t*)mem;

    // Setup head
    GuardHeader* head = (GuardHeader*)(user - sizeof(GuardHeader));
    head->size = size;
    head->offset = headerSize;
    memset(head->pad, GuardHeader::HEAD_SENTINEL, GuardHeader::PAD);

    // Setup buffer
    memset(mem, 0xbc, size);

    // Setup tail
    uint8_t *tail = user + size;
    memset(tail, GuardHeader::TAIL_SENTINEL, GUARD_PAD);
}

void checkGuardedMemory(void* mem, bool poison)
{
    uint8_t* user = (uint8_t*)mem;
    GuardHeader* head = (GuardHeader*)(user - sizeof(GuardHeader));
    uint8_t* tail = user + head->size;

    guardcheck(head->pad, GuardHeader::HEAD_SENTINEL, GuardHeader::PAD);
    guardcheck(tail, GuardHeader::TAIL_SENTINEL, GUARD_PAD);

    if (poison)
        memset(mem, GUARD_HEAP_POISON, (size_t)(head->size));
}

#endif
