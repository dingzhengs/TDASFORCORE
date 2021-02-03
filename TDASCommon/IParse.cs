using System;

namespace TDASCommon
{
    public interface IParse : IDisposable
    {
        int Read(string fileName, long streamOffset, byte[] data, long timestamp, string ip);

        int Read(string filePath);
    }
}
