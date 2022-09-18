using System.ComponentModel;
using System.Reflection.Metadata;
using System.Security.Cryptography;

if ( args.Count() == 0)
{
    usage();
    return;
}

var listTargets = new List<string>();
bool listFlag = false;
bool overrideFlag = false;
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
        if (tag is DataTag) Console.WriteLine(tag);
    }
}

exit:

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
    Tag tag = null;
    if (id == 0) tag = new EndTag();
    else if (id == 1) tag = new VersionTag();
    else if (id == 0x100) tag = new BlankTag();
    else if (id == 0x102) tag = new SpaceTag();
    else if (id == 0x103) tag = new MarkTag();
    else if (id == 0x101)
    {
        tag = new DataTag();

    }
    else
    {
        Console.WriteLine($"Unknown tag {id:X4}");
        return null;
    }
    p += size;
    return tag;
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

}
