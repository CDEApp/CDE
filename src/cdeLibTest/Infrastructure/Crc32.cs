using cdeLib.Infrastructure;

namespace cdeLibTest.Infrastructure
{
    public class Crc32 : IHashAlgorithm
    {
        uint[] _tab;

        public Crc32()
        {
            Init(0x04c11db7);
        }

        public Crc32(uint poly)
        {
            Init(poly);
        }

        void Init(uint poly)
        {
            _tab = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint t = i;
                for (int j = 0; j < 8; j++)
                    if ((t & 1) == 0)
                        t >>= 1;
                    else
                        t = (t >> 1) ^ poly;
                _tab[i] = t;
            }
        }

        public uint Hash(byte[] data)
        {
            uint hash = 0xFFFFFFFF;
            foreach (byte b in data)
                hash = (hash << 8) ^ _tab[b ^ (hash >> 24)];
            return ~hash;
        }
    }
}