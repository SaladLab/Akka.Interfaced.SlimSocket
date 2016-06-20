﻿using System;
using System.IO;
using ProtoBuf.Meta;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class PacketSerializer : PacketSerializerBase
    {
        public PacketSerializer(Data data)
            : base(data)
        {
        }

        public static TypeModel CreateTypeModel()
        {
            RuntimeTypeModel typeModel = TypeModel.Create();
            AkkaSurrogate.Register(typeModel);
            AutoSurrogate.Register(typeModel);
            return typeModel;
        }

        protected override void GetBuffers(
            Stream stream, int pos, int length,
            out ArraySegment<byte> segment0, out ArraySegment<byte> segment1)
        {
            var ms = stream as MemoryStream;
            if (ms != null)
            {
                segment0 = new ArraySegment<byte>(ms.GetBuffer(), pos, length);
                segment1 = default(ArraySegment<byte>);
                return;
            }
            var hs = stream as HeadTailWriteStream;
            if (hs != null)
            {
                hs.GetBuffers(pos, length, out segment0, out segment1);
                return;
            }
            throw new InvalidOperationException("Unknown stream!");
        }

        public static PacketSerializer CreatePacketSerializer()
        {
            return new PacketSerializer(
                new Data(new ProtoBufMessageSerializer(CreateTypeModel())));
        }

        public static PacketSerializer CreatePacketSerializer<TTypeModel>()
            where TTypeModel : TypeModel, new()
        {
            return new PacketSerializer(
                new Data(new ProtoBufMessageSerializer(new TTypeModel())));
        }
    }
}
