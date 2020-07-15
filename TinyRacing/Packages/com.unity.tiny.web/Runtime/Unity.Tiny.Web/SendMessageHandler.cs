using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Unity.Tiny.Web
{
    public struct NativeMessage : IComponentData
    {
        public NativeString512 message;
    }

    public struct NativeMessageInt : IBufferElementData
    {
        public int Value;

        public NativeMessageInt(int value)
        {
            Value = value;
        }
    }

    public struct NativeMessageFloat : IBufferElementData
    {
        public float Value;

        public NativeMessageFloat(float value)
        {
            Value = value;
        }
    }

    public struct NativeMessageByte : IBufferElementData
    {
        public byte Value;

        public NativeMessageByte(byte value)
        {
            Value = value;
        }
    }

    [Preserve]
    public unsafe static class SendMessageHandler
    {
        private delegate void RegisterSendMessageDelegate(string message, int* intArray, int intArrayLength, float* floatArray, int floatArrayLength, byte* byteArray, int byteArrayLength);

        [DllImport("lib_unity_tiny_web", EntryPoint = "RegisterSendMessage")]
        private static extern void RegisterSendMessage(RegisterSendMessageDelegate sendMessageDelegate);

        static SendMessageHandler() => RegisterSendMessage(OnSendMessage);

        [MonoPInvokeCallback]
        private static void OnSendMessage(string message, int* intArray, int intArrayLength, float* floatArray, int floatArrayLength, byte* byteArray, int byteArrayLength)
        {
            using (var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp))
            {
                var messageEntity = entityCommandBuffer.CreateEntity();

                entityCommandBuffer.AddComponent(messageEntity, new NativeMessage { message = new NativeString512(message) });

                var messageIntBuffer = entityCommandBuffer.AddBuffer<NativeMessageInt>(messageEntity);
                CopyArrayToBuffer(messageIntBuffer, intArray, intArrayLength, sizeof(int));

                var messageFloatsBuffer = entityCommandBuffer.AddBuffer<NativeMessageFloat>(messageEntity);
                CopyArrayToBuffer(messageFloatsBuffer, floatArray, floatArrayLength, sizeof(float));

                var messageBytesBuffer = entityCommandBuffer.AddBuffer<NativeMessageByte>(messageEntity);
                CopyArrayToBuffer(messageBytesBuffer, byteArray, byteArrayLength, sizeof(byte));

                entityCommandBuffer.Playback(World.DefaultGameObjectInjectionWorld.EntityManager);
            }
        }

        static void CopyArrayToBuffer<T>(DynamicBuffer<T> buffer, void* source, int length, int sizeOfT) where T : struct
        {
            buffer.ResizeUninitialized(length);
            UnsafeUtility.MemCpy(buffer.GetUnsafePtr(), source, length * sizeOfT);
        }

        [AttributeUsage(AttributeTargets.Method)]
        class MonoPInvokeCallbackAttribute : Attribute {}

        [AttributeUsage(AttributeTargets.Class)]
        class PreserveAttribute : System.Attribute {}
    }
}
