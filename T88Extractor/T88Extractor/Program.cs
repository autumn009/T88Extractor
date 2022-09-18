using System.ComponentModel;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;

if ( args.Count() == 0)
{
    usage();
    return;
}

var listTargets = new List<string>();
bool listFlag = false;
bool overrideFlag = false;
string? filename = null;

foreach (var target in args)
{
    if(target == "-list") listFlag = true;
    else if (target == "-override") overrideFlag = true;
    else
    {
        var dir = Path.GetDirectoryName(target)?? Directory.GetCurrentDirectory();
        var name = Path.GetFileName(target);
        foreach (var item in Directory.GetFiles(dir,name))
        {
            listTargets.Add(item);
        }
    }
}
byte[] bytes = null;
int p = 0;
foreach (var item in listTargets)
{
    Console.WriteLine($"{item}:");
    bytes = File.ReadAllBytes(item);
    p = 0;
    if (checkHeader() == false)
    {
        Console.WriteLine("header not match, Skipped");
        continue;
    }
    for (; ; )
    {
        if (p >= bytes.Length) break;
        var tag = getTag();
        if (tag == null) break;
        if (tag is EndTag) break;
        if (tag is DataTag) analyzeAndSaveData(tag as DataTag);
    }
    Console.WriteLine();
}

exit:;

int getNextByte()
{
    if (p >= bytes.Length) return -1;
    return bytes[p++];
}

int getNextword()
{
    int l = getNextByte();
    int h = getNextByte();
    return l + h * 256;
}

Tag? getTag()
{
    var id = getNextword();
    var size = getNextword();
    Tag? tag = null;
    if (id == 0) tag = new EndTag();
    else if (id == 1) tag = new VersionTag();
    else if (id == 0x100) tag = new BlankTag();
    else if (id == 0x102) tag = new SpaceTag();
    else if (id == 0x103) tag = new MarkTag();
    else if (id == 0x101)
    {
        var dtag = new DataTag();
        dtag.Data = new byte[size];
        for (int i = 0; i < size; i++)
        {
            dtag.Data[i] = (byte)getNextByte();
        }
        return dtag;
    }
    else
    {
        Console.WriteLine($"Unknown tag {id:X4}");
        return null;
    }
    p += size;
    return tag;
}

void analyzeAndSaveData(DataTag? tag)
{
    if (filename == null)
    {
        // seek header
        for (; ; )
        {
            int b = tag.getNextByte();
            if (b < 0) goto eof;
            if( b == 0x3a)
            {
                alalyzeMonitorStyle(tag);
                goto eof;
            }

            if (b == 0xd3) break;
        }
        for (int i = 0; i < 9; i++)
        {
            int b = tag.getNextByte();
            if (b < 0) goto eof;
            if (b != 0xd3) return;
        }
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < 6; i++)
        {
            int b = tag.getNextByte();
            if (b < 0) goto eof;
            if (b == 0) continue;
            if (b < 0x20 || b >= 0x80 || b == ':' || b == '*' || b == '?') b = 'x';
            sb.Append((char)b);
        }
        filename = sb.ToString();
        Console.Write($"{filename}, ");
    }
    else
    {
        if (listFlag) return;   // list only
        int zeroCount = 0;
        int dataCount = 0;
        for(; ; )
        {
            int b = tag.getNextByte();
            if (b < 0) goto eof;
            if (b == 0) zeroCount++;
            dataCount++;
            // SAVE IT
            if (zeroCount == 9)
            {
                Console.Write($"({dataCount}),");
                break;
            }
        }
        filename = null;
    }
eof:;
}

void alalyzeMonitorStyle(DataTag? tag)
{
    var h = tag.getNextByte();
    var l = tag.getNextByte();
    Console.Write($"&h{h:X2}{l:X2}, ");
    var sumAddr = (-h + l) & 0xff;
    var sum1 = tag.getNextByte();
    if( sumAddr != sum1) 
    {
        Console.Write("[ADDR CHECKSUM ERR], ");
        return;
    }
    for (; ; )
    {
        var mark = tag.getNextByte();
        if( mark != 0x3a)
        {
            Console.Write("[NOT DATA HEAD(0x3a) ERR], ");
            return;
        }
        var datasize = tag.getNextByte();
        if (datasize == 0) break;
        if (datasize < 0) break;
        int sum = datasize;
        for (int i = 0; i < datasize; i++)
        {
            var d = tag.getNextByte();
            sum += d;
            // write d
        }
        sum = (-sum) & 0xff;
        var blocksum = tag.getNextByte();
        if (sum != blocksum)
        {
            Console.Write("[BLOCK CHECKSUM ERR], ");
            return;
        }
    }
}

bool checkHeader()
{
    const string header = "PC-8801 Tape Image(T88)\0";
    foreach (var item in header)
    {
        if (item != getNextByte()) return false;
    }
    return true;
}

void usage()
{
    Console.WriteLine("T88Extgractor Path... [-list] [-override]");
    Console.WriteLine("Path: wild card allowed");
    Console.WriteLine("-list: list only");
    Console.WriteLine("-override: erase directory before run");
}

abstract class Tag { }

class EndTag:Tag
{

}

class VersionTag : Tag
{

}

class BlankTag : Tag
{

}

class SpaceTag : Tag
{

}

class MarkTag : Tag
{

}

class DataTag : Tag
{
    public byte[]? Data;
    public int P = 0;
    public int getNextByte()
    {
        if (P >= Data.Length) return -1;
        return Data[P++];
    }
}
