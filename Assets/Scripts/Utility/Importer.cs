using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEngine.UIElements.UxmlAttributeDescription;

public class Importer
{
    // Start is called before the first frame update
    internal class TextureImporter
    {
        byte[] sourceData;//把TIFF文件读到byte数组中
        List<byte> imageData;
        //TIFF文件的各种属性
        bool ByteOrder = true;//true:II  false:MM
        public int ImageWidth = 0;
        public int ImageLength = 0;
        public List<int> BitsPerSample = new List<int>();
        public int PixelBytes = 0;
        public int Compression = 0;
        public int PhotometricInterpretation = 0;
        public List<int> StripOffsets = new List<int>();
        public int RowsPerStrip = 0;
        public List<int> StripByteCounts = new List<int>();
        public float XResolution = 0f;
        public float YResolution = 0f;
        public int ResolutionUnit = 0;
        public int Predictor = 0;
        public List<int> SampleFormat = new List<int>();
        public string DateTime = "";
        public string Software = "";
        /// <summary>
        /// 解码TIFFS
        /// </summary>
        /// <param name="path"></param>
        public Texture3D Decode(string path)
        {
            sourceData = File.ReadAllBytes(path);
            imageData = new List<byte>();

            //首先解码文件头，获得编码方式是大端还是小端，以及第一个IFD的位置
            int pIFD = DecodeIFH();
            Debug.Log(pIFD);
            //然后解码第一个IFD，返回值是下一个IFD的地址
            while (pIFD != 0)
            {
                pIFD = DecodeIFD(pIFD);
            }
            DecodeStrips();

            //List<byte> reversed = new(imageData.Count);
            //for(int z = 0; z < 106; z++)
            //{
            //    for(int y = 0; y < 512;y++)
            //    {
            //        for (int x = 0; x <512; x++)
            //        {
            //            int ind = x + y * 512 + z * 512 * 512;
            //            int ind2 = x + (511-y) * 512 + z * 512 * 512;
            //            //Debug.Log(ind);
            //            reversed.Add(imageData[ind2]);
            //        }
            //    }
            //}
            Debug.Log(imageData.Count); 
            string name = path.Substring(path.LastIndexOf('\\') + 1, path.LastIndexOf('.') - path.LastIndexOf('\\') - 1);
            Texture3D texture3D = new Texture3D(1024, 1024, 1024, TextureFormat.R8, false);
            texture3D.SetPixelData(imageData.ToArray(), 0);
            texture3D.Apply();
#if UNITY_EDITOR
            AssetDatabase.DeleteAsset("Assets/Textures/" + name + ".Asset");
            AssetDatabase.CreateAsset(texture3D, "Assets/Textures/" + name + ".Asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
            return texture3D;
        }
        /// <summary>
        /// 解码v3dpbd
        /// </summary>
        /// <param name="path"></param>
        public Texture3D DecodePBD(string path)
        {
            sourceData = File.ReadAllBytes(path);
            byte[]  targetData;
            Debug.Log(sourceData.Length);
            string formatkey = "v3d_volume_pkbitdf_encod";
            Debug.Log(GetString(formatkey.Length, 1));
            bool b_swap = GetString(formatkey.Length,1) == "L";

            int deo = GetInt(formatkey.Length + 1, 2);

            Debug.Log(GetInt(formatkey.Length + 1 + 2, 4));
            Debug.Log(GetInt(formatkey.Length + 1 + 6, 4));
            Debug.Log(GetInt(formatkey.Length + 1 + 10, 4));
            Debug.Log(GetInt(formatkey.Length + 1 + 14, 4));

            int[] sz= new int[4];
            for(int i = 0; i < 4; i++)
            {
                sz[i] = GetInt(formatkey.Length + 1 + 2 + i * 4, 4);
            }

            targetData= new byte[sz[0] * sz[1] * sz[2] * sz[3]];
            int head = formatkey.Length + 1 + 2 + 16;
            int sourceLength = sourceData.Length - head;

            int cp = 0;
            int dp = 0;
            const byte mask = 0x0003;
            byte p0, p1, p2, p3;
            byte value = 0;
            byte pva = 0;
            byte pvb = 0;
            int leftToFill = 0;
            int fillNumber = 0;
            int tofillPos =0;
            byte sourceChar = 0;
            int decompressionPrior = 0;
            while (cp < sourceLength)
            {
                value = sourceData[cp+head];

                if (value < 33)
                {
                    // Literal 0-32
                    byte count = (byte)(value + 1);
                    for (int j = cp + 1; j < cp + 1 + count; j++)
                    {
                        targetData[dp++] = sourceData[j+head];
                    }
                    cp += (count + 1);
                    decompressionPrior = targetData[dp - 1];
                }
                else if (value < 128)
                {
                    // Difference 33-127
                    leftToFill = value - 32;
                    while (leftToFill > 0)
                    {
                        fillNumber = (leftToFill < 4 ? leftToFill : 4);
                        sourceChar = sourceData[(++cp) + head];
                        tofillPos = dp;
                        p0 = (byte)(sourceChar & mask);
                        sourceChar >>= 2;
                        p1 = (byte)(sourceChar & mask);
                        sourceChar >>= 2;
                        p2 = (byte)(sourceChar & mask);
                        sourceChar >>= 2;
                        p3 = (byte)(sourceChar & mask);
                        pva = (byte)((p0 == 3 ? -1 : p0) + decompressionPrior);

                        targetData[tofillPos] = pva;

                        if (fillNumber > 1)
                        {
                            tofillPos++;
                            pvb = (byte)(pva + (p1 == 3 ? -1 : p1));
                            targetData[tofillPos] = pvb;
                            if (fillNumber > 2)
                            {
                                tofillPos++;
                                pva = (byte)((p2 == 3 ? -1 : p2) + pvb);
                                targetData[tofillPos] = pva;
                                if (fillNumber > 3)
                                {
                                    tofillPos++;
                                    targetData[tofillPos] = (byte)((p3 == 3 ? -1 : p3) + pva);
                                }
                            }
                        }

                        decompressionPrior = targetData[tofillPos];
                        dp += fillNumber;
                        leftToFill -= fillNumber;
                    }
                    cp++;
                }
                else
                {
                    // Repeat 128-255
                    byte repeatCount = (byte)(value - 127);
                    byte repeatValue = sourceData[++cp + head];

                    for (int j = 0; j < repeatCount; j++)
                    {
                        targetData[dp++] = repeatValue;
                    }
                    decompressionPrior = repeatValue;
                    cp++;
                }
            }

            Texture3D texture3D = new Texture3D(sz[0], sz[1], sz[2], TextureFormat.R8, false);
            texture3D.SetPixelData(targetData, 0);
            texture3D.wrapMode = TextureWrapMode.Clamp;
            texture3D.Apply();
            string name = path.Substring(path.LastIndexOf('\\') + 1, path.LastIndexOf('.') - path.LastIndexOf('\\')-1);
#if UNITY_EDITOR
            AssetDatabase.DeleteAsset("Assets/Textures/" + name + ".Asset");
            AssetDatabase.CreateAsset(texture3D, "Assets/Textures/" + name + ".Asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
            return texture3D;
        }
        private int DecodeIFH()
        {
            string byteOrder = GetString(0, 2);
            if (byteOrder == "II")
                ByteOrder = true;
            else if (byteOrder == "MM")
                ByteOrder = false;
            else
                throw new UnityException("The order value is not II or MM.");
            int Version = GetInt(2, 2);
            if (Version != 42)
                throw new UnityException("Not TIFF.");

            return GetInt(4, 4);
        }
        public int DecodeIFD(int Pos)
        {
            int n = Pos;
            int DECount = GetInt(n, 2);
            n += 2;
            for (int i = 0; i < DECount; i++)
            {
                DecodeDE(n);
                n += 12;
            }
            //已获得每条扫描线位置，大小，压缩方式和数据类型，接下来进行解码
            
            int pNext = GetInt(n, 4);
            Debug.Log(pNext);
            return pNext;
        }
        private void DecodeDE(int Pos)
        {
            int TagIndex = GetInt(Pos, 2);
            int TypeIndex = GetInt(Pos + 2, 2);
            int Count = GetInt(Pos + 4, 4);
            //Debug.Log("Tag: " + Tag(TagIndex) + " DataType: " + TypeArray[TypeIndex].name + " Count: " + Count);

            //先把找到数据的位置
            int pData = Pos + 8;
            int totalSize = TypeArray[TypeIndex].size * Count;
            if (totalSize > 4)
                pData = GetInt(pData, 4);

            //再根据Tag把值读出并存起来
            GetDEValue(TagIndex, TypeIndex, Count, pData);
        }
        private void GetDEValue(int TagIndex, int TypeIndex, int Count, int pdata)
        {
            int typesize = TypeArray[TypeIndex].size;
            switch (TagIndex)
            {
                case 254: break;//NewSubfileType
                case 255: break;//SubfileType
                case 256://ImageWidth   //√
                    ImageWidth = GetInt(pdata, typesize); break;
                case 257://ImageLength  //√
                    if (TypeIndex == 3)//short
                        ImageLength = GetInt(pdata, typesize); break;
                case 258://BitsPerSample    //√
                    for (int i = 0; i < Count; i++)
                    {
                        int v = GetInt(pdata + i * typesize, typesize);
                        BitsPerSample.Add(v);
                        PixelBytes += v / 8;
                    }
                    break;
                case 259: //Compression //√
                    Compression = GetInt(pdata, typesize); break;
                case 262: //PhotometricInterpretation   //√
                    PhotometricInterpretation = GetInt(pdata, typesize); break;
                case 273://StripOffsets //√
                    for (int i = 0; i < Count; i++)
                    {
                        int v = GetInt(pdata + i * typesize, typesize);
                        StripOffsets.Add(v);
                    }
                    break;
                case 274: break;//Orientation   //√
                case 277: break;//SamplesPerPixel   //√
                case 278://RowsPerStrip //√
                    RowsPerStrip = GetInt(pdata, typesize); break;
                case 279://StripByteCounts  //√
                    for (int i = 0; i < Count; i++)
                    {
                        int v = GetInt(pdata + i * typesize, typesize);
                        StripByteCounts.Add(v);
                    }
                    break;
                case 282: //XResolution //√
                    XResolution = GetRational(pdata); break;
                case 283://YResolution  //√
                    YResolution = GetRational(pdata); break;
                case 284: break;//PlanarConfig  //√
                case 296://ResolutionUnit   //√
                    ResolutionUnit = GetInt(pdata, typesize); break;
                case 305://Software
                    Software = GetString(pdata, typesize); break;
                case 306://DateTime
                    DateTime = GetString(pdata, typesize); break;
                case 315: break;//Artist
                case 317: //Differencing Predictor
                    Predictor = GetInt(pdata, typesize); break;
                case 320: break;//ColorDistributionTable
                case 338: break;//ExtraSamples
                case 339: //SampleFormat
                    for (int i = 0; i < Count; i++)
                    {
                        int v = GetInt(pdata + i * typesize, typesize);
                        SampleFormat.Add(v);
                    }
                    break;

                default: break;
            }
        }
        private void DecodeStrips()
        {
            int pStrip = 0;
            int size = 0;

            ////tex = new Texture2D(ImageWidth, ImageLength, TextureFormat.RGBA32, false);

            ////Color[] colors = new Color[ImageWidth * ImageLength];
            //int stripLength = ImageWidth * RowsPerStrip * BitsPerSample.Count * BitsPerSample[1] / 8;
            Debug.Log("StripOffsets.Count:" + StripOffsets.Count);
            for (int y = 0; y < StripOffsets.Count; y++)
            {
                pStrip = StripOffsets[y];//起始位置
                size = StripByteCounts[y];//读取长度
                for (int i = 0; i < size; i++)
                {
                    imageData.Add(sourceData[pStrip + i]);
                }
            }
            Debug.Log("count:"+imageData.Count);
            //if (Compression == 5)
            //{

        }

        private int GetInt(int startPos, int Length)
        {
            int value = 0;
            if (ByteOrder)// "II")
                for (int i = 0; i < Length; i++) value |= sourceData[startPos + i] << i * 8;
            else // "MM")
                for (int i = 0; i < Length; i++) value |= sourceData[startPos + Length - 1 - i] << i * 8;
            return value;
        }
        private float GetRational(int startPos)
        {
            int A = GetInt(startPos, 4);
            int B = GetInt(startPos + 4, 4);
            return A / B;
        }
        private float GetFloat(byte[] b, int startPos)
        {
            byte[] byteTemp;
            if (ByteOrder)// "II")
                byteTemp = new byte[] { b[startPos], b[startPos + 1], b[startPos + 2], b[startPos + 3] };
            else
                byteTemp = new byte[] { b[startPos + 3], b[startPos + 2], b[startPos + 1], b[startPos] };
            float fTemp = BitConverter.ToSingle(byteTemp, 0);
            return fTemp;
        }
        private string GetString(int startPos, int Length)//II和MM对String没有影响
        {
            string tmp = "";
            for (int i = 0; i < Length; i++)
                tmp += (char)sourceData[startPos+i];
            return tmp;
        }
        static private DType[] TypeArray = {
            new DType("???",0),
                new DType("byte",1), //8-bit unsigned integer
                new DType("ascii",1),//8-bit byte that contains a 7-bit ASCII code; the last byte must be NUL (binary zero)
                new DType("short",2),//16-bit (2-byte) unsigned integer.
                new DType("long",4),//32-bit (4-byte) unsigned integer.
                new DType("rational",8),//Two LONGs: the first represents the numerator of a fraction; the second, the denominator.
                new DType("sbyte",1),//An 8-bit signed (twos-complement) integer
                new DType("undefined",1),//An 8-bit byte that may contain anything, depending on the definition of the field
                new DType("sshort",1),//A 16-bit (2-byte) signed (twos-complement) integer.
                new DType("slong",1),// A 32-bit (4-byte) signed (twos-complement) integer.
                new DType("srational",1),//Two SLONG’s: the first represents the numerator of a fraction, the second the denominator.
                new DType("float",4),//Single precision (4-byte) IEEE format
                new DType("double",8)//Double precision (8-byte) IEEE format
        };


        struct DType
        {
            public DType(string n, int s)
            {
                name = n;
                size = s;
            }
            public string name;
            public int size;
        }
    }

    public string path;
    void Start()
    {
        //TextureImporter importer = new TextureImporter();
        //string name = "00029";
        //importer.Decode(@"C:\Users\80121\Desktop\00011_P001_T01-S002_MFG_R0460_WY-20220415_XJ.tif", "WY-20220415");
        //path = "Z:\\gold166\\p_checked6_mouse_RGC_uw\\sv_080926a\\080926a.tif.v3dpbd";
        ////importer.DecodePBD(path);
        //importer.DecodePBD(path)
    }

    public Texture3D Load(string path)
    {
        TextureImporter importer = new TextureImporter();
        if (path.Substring(path.LastIndexOf('.') + 1) == "tiff")
        {
            return importer.Decode(path);
        }
        else
        {
            return importer.DecodePBD(path);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
