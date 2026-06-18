// Created by Alexander Tkachenko aka ALT, 2026 https://www.artstation.com/alternative_ms
// This solusion is optimized for WebGL build work
// a 'handmade' decompressor, instead of a System.IO.Compression , DeflateStream and GZipStream, because they crash on WebGL at runtime
using System;
using System.Collections.Generic;
using UnityEngine;

public static class WebGLPureInflate
{
    private static readonly int[] LengthBases = { 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258 };
    private static readonly int[] LengthExtraBits = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0 };
    private static readonly int[] DistanceBases = { 1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577 };
    private static readonly int[] DistanceExtraBits = { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13 };
    private static readonly int[] CodeOrder = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

    private struct HuffmanTree
    {
        public int[] Counts;
        public int[] Symbols;
    }

    public static byte[] Inflate(byte[] input, int offset, int length)
    {
        List<byte> output = new List<byte>();
        int bitBuffer = 0;
        int bitCount = 0;
        int inPtr = offset;
        int endPtr = offset + length;

        Func<int, int> readBits = (n) => {
            while (bitCount < n)
            {
                if (inPtr >= endPtr) break;
                bitBuffer |= (input[inPtr++] & 0xFF) << bitCount;
                bitCount += 8;
            }
            int res = bitBuffer & ((1 << n) - 1);
            bitBuffer >>= n;
            bitCount -= n;
            return res;
        };

        Func<HuffmanTree, int> decodeSymbol = (tree) => {
            int code = 0;
            int first = 0;
            int index = 0;
            for (int len = 1; len < tree.Counts.Length; len++)
            {
                code |= readBits(1);
                int count = tree.Counts[len];
                if (code - first < count) return tree.Symbols[index + (code - first)];
                index += count;
                first += count;
                first <<= 1;
                code <<= 1;
            }
            return -1;
        };

        Action<int[], HuffmanTree> buildTree = (lengths, tree) => {
            for (int i = 0; i < lengths.Length; i++) if (lengths[i] > 0) tree.Counts[lengths[i]]++;
            int index = 0;
            for (int len = 1; len < tree.Counts.Length; len++)
            {
                for (int i = 0; i < lengths.Length; i++)
                {
                    if (lengths[i] == len) tree.Symbols[index++] = i;
                }
            }
        };

        bool isLastBlock = false;
        while (!isLastBlock)
        {
            isLastBlock = readBits(1) == 1;
            int blockType = readBits(2);

            if (blockType == 0)
            { // Uncompressed
                bitBuffer = 0; bitCount = 0;
                int len = input[inPtr++] | (input[inPtr++] << 8);
                inPtr += 2; // Skip nlen
                for (int i = 0; i < len; i++) output.Add(input[inPtr++]);
            }
            else if (blockType == 1 || blockType == 2)
            {
                HuffmanTree litTree = new HuffmanTree { Counts = new int[16], Symbols = new int[288] };
                HuffmanTree distTree = new HuffmanTree { Counts = new int[16], Symbols = new int[32] };

                if (blockType == 1)
                { // Fixed Huffman
                    int[] litLens = new int[288];
                    for (int i = 0; i < 144; i++) litLens[i] = 8;
                    for (int i = 144; i < 256; i++) litLens[i] = 9;
                    for (int i = 256; i < 280; i++) litLens[i] = 7;
                    for (int i = 280; i < 288; i++) litLens[i] = 8;
                    buildTree(litLens, litTree);
                    int[] distLens = new int[32];
                    for (int i = 0; i < 32; i++) distLens[i] = 5;
                    buildTree(distLens, distTree);
                }
                else
                { // Dynamic Huffman
                    int hlit = readBits(5) + 257;
                    int hdist = readBits(5) + 1;
                    int hclen = readBits(4) + 4;
                    int[] codeLens = new int[19];
                    for (int i = 0; i < hclen; i++) codeLens[CodeOrder[i]] = readBits(3);
                    HuffmanTree codeTree = new HuffmanTree { Counts = new int[16], Symbols = new int[19] };
                    buildTree(codeLens, codeTree);
                    int[] allLens = new int[hlit + hdist];
                    int index = 0;
                    while (index < allLens.Length)
                    {
                        int sym = decodeSymbol(codeTree);
                        if (sym < 16) allLens[index++] = sym;
                        else
                        {
                            int rep = 0, len = 0;
                            if (sym == 16) { rep = readBits(2) + 3; len = allLens[index - 1]; }
                            else if (sym == 17) rep = readBits(3) + 3;
                            else if (sym == 18) rep = readBits(7) + 11;
                            for (int i = 0; i < rep; i++) allLens[index++] = len;
                        }
                    }
                    int[] litLens = new int[hlit]; Array.Copy(allLens, 0, litLens, 0, hlit);
                    buildTree(litLens, litTree);
                    int[] distLens = new int[hdist]; Array.Copy(allLens, hlit, distLens, 0, hdist);
                    buildTree(distLens, distTree);
                }

                while (true)
                {
                    int sym = decodeSymbol(litTree);
                    if (sym < 256) output.Add((byte)sym);
                    else if (sym == 256) break;
                    else
                    {
                        sym -= 257;
                        int len = LengthBases[sym] + readBits(LengthExtraBits[sym]);
                        int distSym = decodeSymbol(distTree);
                        int dist = DistanceBases[distSym] + readBits(DistanceExtraBits[distSym]);
                        int startIdx = output.Count - dist;
                        for (int i = 0; i < len; i++) output.Add(output[startIdx + i]);
                    }
                }
            }
        }

        //Debug.Log("Inflate | output");
        return output.ToArray();
    }
}