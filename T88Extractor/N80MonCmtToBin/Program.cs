// convert PC-8001 N-Basic monitor saved file to binary

if( args.Length != 2)
{
    Console.WriteLine("usage: N80MonCmtToBun SRC_FILE_NAME DST_FILE_NAME");
    return;
}

using var streamR = File.OpenRead(args[0]);
using var streamW = File.OpenWrite(args[1]);

streamR.ReadByte();    // skip 3a
var hi = streamR.ReadByte();
var lo = streamR.ReadByte();
streamR.ReadByte();    // skip checksum
Console.WriteLine($"Start address: {hi:X2}{lo:X2}");

for (; ;)
{
    streamR.ReadByte();    // skip 3a
    var count = streamR.ReadByte();
    if (count <= 0) break;
    for (int i = 0; i < count; i++)
    {
        var d = streamR.ReadByte();
        streamW.WriteByte((byte)d);
    }
    streamR.ReadByte();    // skip checksum
}
Console.WriteLine("Done");
