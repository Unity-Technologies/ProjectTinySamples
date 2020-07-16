#if ENABLE_PLAYERCONNECTION

using static System.Text.Encoding;
using Unity.Burst;

namespace Unity.Development.PlayerConnection
{
    public class Logger
    {
        private static readonly SharedStatic<MessageStreamBuilder> buffer = SharedStatic<MessageStreamBuilder>.GetOrCreate<Logger, MessageStreamBuilder>();

        public unsafe static void Initialize()
        {
            MessageStreamManager.RegisterExternalBufferSend((MessageStreamBuilder*)buffer.UnsafeDataPointer);
        }

        public static void Log(string text)
        {
            int textBytes = UTF8.GetByteCount(text);

            unsafe
            {
                byte* textBuf = stackalloc byte[textBytes];

                fixed (char* t = text)
                {
                    UTF8.GetBytes(t, text.Length, textBuf, textBytes);
                    Log(textBuf, textBytes);
                }
            }
        }

        public unsafe static void Log(byte* textUtf8, int textBytes)
        {
            // If IsExternal wasn't set, we know the buffer wasn't initialized
            if (!buffer.Data.IsExternal)
                return;

            buffer.Data.MessageBegin(EditorMessageIds.kLog);
            buffer.Data.WriteData(textBytes);
            buffer.Data.WriteRaw(textUtf8, textBytes);
            buffer.Data.MessageEnd();
        }
    }
}

#endif
