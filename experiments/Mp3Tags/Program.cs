using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;

namespace Mp3Tags
{
    class Program
    {
        static void Main(string[] args)
        {
            TagLib.Id3v2.TextInformationFrame.ForceOverrideReadingTextType = StringType.UTF8;
            TagLib.Id3v2.Tag.DefaultEncoding = StringType.UTF8;
            TagLib.Id3v2.Tag.ForceDefaultEncoding = true;

            Parallel.ForEach(
                Directory.EnumerateFiles("c:\\temp\\jaychou\\"),
                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                file =>
                {
                    using (var audioFile = TagLib.File.Create(file))
                    {
                        Console.WriteLine("{0} {1} {2} {3}", file, audioFile.Tag.Album, audioFile.Tag.Title, string.Join(",", audioFile.Tag.AlbumArtists));
                        audioFile.Save();
                    }
                });

            //using (var file = TagLib.File.Create("c:\\temp\\001.mp3"))
            //{
            //    Console.WriteLine(file.Tag.Album);
            //}
        }
    }
}
