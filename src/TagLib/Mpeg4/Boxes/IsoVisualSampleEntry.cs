using System;

namespace TagLib.Mpeg4
{
   public class IsoVisualSampleEntry : IsoSampleEntry, IVideoCodec
   {
#region Private Properties
      private ushort width;
      private ushort height;
      // private BoxList children;
#endregion
      
      
#region Constructors
      
      public IsoVisualSampleEntry (BoxHeader header, File file, Box handler) : base (header, file, handler)
      {
         file.Seek (base.DataOffset + 16);
         width  = file.ReadBlock (2).ToUShort ();
         height = file.ReadBlock (2).ToUShort ();
         // TODO: What are the children anyway?
         // children = LoadChildren (file);
      }
#endregion
      
      
#region Public Properties
      protected override long    DataOffset {get {return base.DataOffset + 62;}}
      // public    override BoxList Children   {get {return children;}}
#endregion
      
#region IVideoCodec Properties
      public int VideoWidth  {get {return width;}}
      public int VideoHeight {get {return height;}}
      public string Description {get {return "MPEG-4 Video (" + BoxType.ToString () + ")";}}
      public MediaTypes MediaTypes {get {return MediaTypes.Video;}}
      public TimeSpan Duration {get {return TimeSpan.Zero;}}
#endregion
   }
}