// Created by Alexander Tkachenko aka ALT, 2026 https://www.artstation.com/alternative_ms
// This solusion is optimized for WebGL build work
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AsepriteReader : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AsepriteRuntimeLoader runtimeLoader;

    public AsepriteRuntimeLoader RuntimeLoader
    {
        get => runtimeLoader;
        set => runtimeLoader = value;
    }

    [System.Serializable]
    public struct AseFileHeader
    {
        public int Width;
        public int Height;
        public int TotalFrames;
    }

    public AseFileHeader ReadHeader(byte[] fileBytes)
    {
        AseFileHeader header = new AseFileHeader();

        using (MemoryStream ms = new MemoryStream(fileBytes))
        {
            using (BinaryReader reader = new BinaryReader(ms))
            {
                if (fileBytes.Length < 128)
                {
                    Debug.Log("Exception : File size is too small");
                    throw new Exception("File size is too small");
                }

                reader.BaseStream.Position = 4;
                if (reader.ReadUInt16() != 0xA5E0)
                {
                    Debug.Log("Exception : Invalid Magic Number Aseprite");
                    throw new Exception("Invalid Magic Number Aseprite");
                }

                header.TotalFrames = reader.ReadUInt16();
                Debug.Log("TotalFrames : " + header.TotalFrames);

                header.Width = reader.ReadUInt16();
                header.Height = reader.ReadUInt16();

                reader.BaseStream.Position = 12;
                if (reader.ReadUInt16() != 32)
                {
                    Debug.Log("Exception : Not 32-bit mode");
                    throw new NotSupportedException("File must be a 32-bit color mode");
                }
            }
        }

        return header;
    }

    public Color32[] ParseFramePixels(byte[] fileBytes, int targetFrameIndex, int canvasW, int canvasH, out float durationSeconds)
    {
        Debug.Log("ParseFramePixels | Target Frame: {targetFrameIndex}");

        durationSeconds = 0.1f;
        Color32[] blendedCanvas = new Color32[canvasW * canvasH];
        for (int i = 0; i < blendedCanvas.Length; i++) blendedCanvas[i] = new Color32(0, 0, 0, 0);

        using (MemoryStream ms = new MemoryStream(fileBytes))
        {
            using (BinaryReader reader = new BinaryReader(ms))
            {
                long nextFrameOffset = 128;

                Dictionary<string, byte[]> celCache = new Dictionary<string, byte[]>();
                Dictionary<string, ushort[]> celSizeCache = new Dictionary<string, ushort[]>();
                Dictionary<string, short[]> celPosCache = new Dictionary<string, short[]>();

                for (int f = 0; f <= targetFrameIndex; f++)
                {
                    if (nextFrameOffset >= fileBytes.Length)
                    {
                        Debug.Log("ParseFrame | nextFrameOffset >= fileBytes.Length");
                        break;
                    }

                    reader.BaseStream.Position = nextFrameOffset;

                    long frameStartPos = reader.BaseStream.Position;
                    uint frameBytes = reader.ReadUInt32();
                    ushort frameMagic = reader.ReadUInt16();

                    if (frameMagic != 0xF1FA)
                    {
                        Debug.Log("ParseFrame | frameMagic != 0xF1FA");
                        break;
                    }

                    ushort oldChunksCount = reader.ReadUInt16();
                    ushort frameDurationMs = reader.ReadUInt16();
                    reader.BaseStream.Position = frameStartPos + 12;

                    uint newChunksCount = reader.ReadUInt32();
                    uint chunksToRead = newChunksCount == 0 ? oldChunksCount : newChunksCount;

                    nextFrameOffset = frameStartPos + frameBytes;

                    bool isTargetFrame = (f == targetFrameIndex);
                    if (isTargetFrame)
                    {
                        durationSeconds = frameDurationMs / 1000f;
                        Debug.Log($"ParseFrame | Target Found! Chunks: {chunksToRead}");
                    }

                    long nextChunkOffset = frameStartPos + 16;

                    for (int c = 0; c < chunksToRead; c++)
                    {
                        if (nextChunkOffset >= nextFrameOffset) break;
                        reader.BaseStream.Position = nextChunkOffset;

                        long chunkStartPos = reader.BaseStream.Position;
                        uint chunkSize = reader.ReadUInt32();
                        ushort chunkType = reader.ReadUInt16();

                        nextChunkOffset = chunkStartPos + chunkSize;

                        if (chunkType == 0x2005) // Cel Chunk
                        {
                            ushort layerIndex = reader.ReadUInt16();
                            short xPos = reader.ReadInt16();
                            short yPos = reader.ReadInt16();
                            byte opacity = reader.ReadByte();
                            ushort celType = reader.ReadUInt16();
                            short zIndex = reader.ReadInt16();
                            reader.ReadBytes(5);

                            byte[] decompressedPixels = null;
                            ushort celWidth = 0;
                            ushort celHeight = 0;

                            if (celType == 0) // Raw Cel
                            {
                                celWidth = reader.ReadUInt16();
                                celHeight = reader.ReadUInt16();
                                decompressedPixels = reader.ReadBytes(celWidth * celHeight * 4);
                            }
                            else if (celType == 1) // Linked Cel
                            {
                                ushort linkFrameIndex = reader.ReadUInt16();
                                string targetKey = $"{layerIndex}_{linkFrameIndex}";

                                if (celCache.ContainsKey(targetKey))
                                {
                                    decompressedPixels = celCache[targetKey];

                                    celWidth = celSizeCache[targetKey][0];
                                    celHeight = celSizeCache[targetKey][1];

                                    xPos = celPosCache[targetKey][0];
                                    yPos = celPosCache[targetKey][1];
                                }
                            }
                            else if (celType == 2) // Compressed Cel
                            {
                                celWidth = reader.ReadUInt16();
                                celHeight = reader.ReadUInt16();

                                long currentPos = reader.BaseStream.Position;
                                int compressedLength = (int)(chunkSize - (currentPos - chunkStartPos));

                                if (compressedLength > 0)
                                {
                                    //Debug.Log($"ParseFrame | Decompressing Cel L:{layerIndex} Size:{compressedLength}");
                                    decompressedPixels = DecompressZlib(reader.BaseStream, compressedLength);
                                }
                            }

                            if (decompressedPixels != null && celWidth > 0 && celHeight > 0)
                            {
                                if (celType == 0 || celType == 2)
                                {
                                    string cacheKey = $"{layerIndex}_{f}";
                                    celCache[cacheKey] = decompressedPixels;
                                    celSizeCache[cacheKey] = new ushort[] { celWidth, celHeight };
                                    celPosCache[cacheKey] = new short[] { xPos, yPos };
                                }

                                if (isTargetFrame)
                                {
                                    BlendCelToCanvas(blendedCanvas, canvasW, canvasH, decompressedPixels, celWidth, celHeight, xPos, yPos, opacity);
                                }
                            }
                        }
                    }
                }
            }
        }

        Debug.Log("ParseFrame | Done successfully");
        return blendedCanvas;
    }

    private byte[] DecompressZlib(Stream stream, int compressedLength)
    {
        //Debug.Log("DecompressZlib | " + stream.Length + " | " + compressedLength);

        if (compressedLength <= 6)
        {
            Debug.Log("DecompressZlib | compressedLength <= 6");
            return new byte[0];
        }

        byte[] compressedBuffer = new byte[compressedLength];
        int bytesRead = stream.Read(compressedBuffer, 0, compressedLength);
        //Debug.Log("DecompressZlib | bytesRead : " + bytesRead);
        int stripOffset = 0;
        if (bytesRead >= 2 && compressedBuffer[0] == 0x78)
        {
            stripOffset = 2; // cut Zlib header
        }

        int pureDeflateLength = bytesRead - stripOffset - 4; // cut checksum Adler32 at the end
        if (pureDeflateLength <= 0)
        {
            Debug.Log("DecompressZlib | pureDeflateLength <= 0");
            return new byte[0];
        }

        try
        {
            //Debug.Log("DecompressZlib | try WebGLPureInflate.Inflate");
            return WebGLPureInflate.Inflate(compressedBuffer, stripOffset, pureDeflateLength); // use 'handmade' decompressor that don't crash on WebGL
        }
        catch (Exception ex)
        {
            Debug.Log("DecompressZlib | ERROR : " + ex.Message);
            return new byte[0];
        }
    }

    // support a basic color blands for now
    private void BlendCelToCanvas(Color32[] canvas, int canvasW, int canvasH, byte[] celBytes, int celW, int celH, int xOffset, int yOffset, byte celOpacity)
    {
        int byteIndex = 0;
        float opacityFactor = celOpacity / 255f;

        for (int y = 0; y < celH; y++)
        {
            for (int x = 0; x < celW; x++)
            {
                if (byteIndex + 3 >= celBytes.Length) return;

                byte rSrc = celBytes[byteIndex++];
                byte gSrc = celBytes[byteIndex++];
                byte bSrc = celBytes[byteIndex++];
                byte aSrc = (byte)(celBytes[byteIndex++] * opacityFactor);

                int canvasX = x + xOffset;
                int canvasY = canvasH - 1 - (y + yOffset);

                if (canvasX >= 0 && canvasX < canvasW && canvasY >= 0 && canvasY < canvasH)
                {
                    int pixelIndex = canvasY * canvasW + canvasX;

                    if (aSrc == 0) continue;

                    Color32 dst = canvas[pixelIndex];
                    if (dst.a == 0)
                    {
                        canvas[pixelIndex] = new Color32(rSrc, gSrc, bSrc, aSrc);
                    }
                    else
                    {
                        float srcA = aSrc / 255f;
                        float dstA = dst.a / 255f;
                        float outA = srcA + dstA * (1f - srcA);

                        if (outA > 0)
                        {
                            byte rOut = (byte)((rSrc * srcA + dst.r * dstA * (1f - srcA)) / outA);
                            byte gOut = (byte)((gSrc * srcA + dst.g * dstA * (1f - srcA)) / outA);
                            byte bOut = (byte)((bSrc * srcA + dst.b * dstA * (1f - srcA)) / outA);
                            canvas[pixelIndex] = new Color32(rOut, gOut, bOut, (byte)(outA * 255f));
                        }
                    }
                }
            }
        }
    }
}