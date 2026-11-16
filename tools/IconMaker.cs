using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;

public static class IconMaker {
 public static void Main(string[] args) {
  string folder=args.Length>0?args[0]:"assets"; Directory.CreateDirectory(folder);
  int[] sizes={16,24,32,48,64,128,256}; var images=new List<byte[]>();
  foreach(int size in sizes) using(var bmp=new Bitmap(size,size,PixelFormat.Format32bppArgb)) {
   using(var g=Graphics.FromImage(bmp)) {
    g.SmoothingMode=SmoothingMode.AntiAlias; g.ScaleTransform(size/256f,size/256f);
    using(var path=Round(8,8,240,240,60)) using(var bg=new LinearGradientBrush(new Rectangle(0,0,256,256),Color.FromArgb(39,56,54),Color.FromArgb(15,24,29),60f)) g.FillPath(bg,path);
    using(var ring=new Pen(Color.FromArgb(76,110,98),9)) g.DrawEllipse(ring,49,49,158,158);
    using(var ring=new Pen(Color.FromArgb(167,227,188),9)) { ring.StartCap=LineCap.Round; ring.EndCap=LineCap.Round; g.DrawArc(ring,49,49,158,158,-90,245); }
    using(var stem=new Pen(Color.FromArgb(167,227,188),10)) { stem.StartCap=LineCap.Round; stem.EndCap=LineCap.Round; g.DrawLine(stem,128,167,128,113); }
    using(var b=new SolidBrush(Color.FromArgb(167,227,188))) {
     using(var leaf=new GraphicsPath()) { leaf.AddBezier(128,137,93,142,87,113,88,100); leaf.AddBezier(88,100,112,101,131,113,128,137); g.FillPath(b,leaf); }
     using(var leaf=new GraphicsPath()) { leaf.AddBezier(128,120,128,90,147,82,171,82); leaf.AddBezier(171,82,172,106,152,123,128,120); g.FillPath(b,leaf); }
    }
   }
   using(var ms=new MemoryStream()) { bmp.Save(ms,ImageFormat.Png); images.Add(ms.ToArray()); }
   if(size==256) bmp.Save(Path.Combine(folder,"app.png"),ImageFormat.Png);
  }
  using(var writer=new BinaryWriter(File.Create(Path.Combine(folder,"app.ico")))) {
   writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length); int offset=6+16*sizes.Length;
   for(int i=0;i<sizes.Length;i++) { writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(images[i].Length); writer.Write(offset); offset+=images[i].Length; }
   foreach(var bytes in images) writer.Write(bytes);
  }
 }
 static GraphicsPath Round(int x,int y,int w,int h,int radius) { var p=new GraphicsPath(); int d=radius*2; p.AddArc(x,y,d,d,180,90); p.AddArc(x+w-d,y,d,d,270,90); p.AddArc(x+w-d,y+h-d,d,d,0,90); p.AddArc(x,y+h-d,d,d,90,90); p.CloseFigure(); return p; }
}
