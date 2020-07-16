#include <Unity/Runtime.h>
#include <emscripten.h>

typedef void (*SendMessageDelegate)(const char* message, const int* intArray, const int intArrayLength, const float* floatArray, const int floatArrayLength, const unsigned char* byteArray, const int byteArrayLength);

static SendMessageDelegate sSendMessageDelegate;

DOTS_EXPORT(void)
RegisterSendMessage(SendMessageDelegate delegate)
{
    sSendMessageDelegate = delegate;
}

DOTS_EXPORT(void)
SendMessage (const char* message, const int* intArray, const int intArrayLength, const float* floatArray, const int floatArrayLength, const unsigned char* byteArray, const int byteArrayLength)
{
    sSendMessageDelegate(message, intArray, intArrayLength, floatArray, floatArrayLength, byteArray, byteArrayLength);
}

