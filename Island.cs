using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public sealed class IslandPresence {
 public bool Enabled=true, Pinned;
 public bool Sleeping { get; private set; }
 double lastInteraction, hoverStart=-1;
 public void Wake(double now) { Sleeping=false; lastInteraction=now; hoverStart=-1; }
 public bool Update(double now,bool overWindow,bool overHotspot,bool busy) {
  if(!Enabled) return false;
  if(Sleeping) {
   if(!overHotspot) hoverStart=-1;
   else if(hoverStart<0) hoverStart=now;
   else if(now-hoverStart>=.14) { Wake(now); return true; }
  } else {
   if(overWindow || busy || Pinned) lastInteraction=now;
   if(!Pinned && !busy && now-lastInteraction>=1.8) { Sleeping=true; hoverStart=-1; return true; }
  }
  return false;
 }
}

public sealed class FocusIsland : Form {
 readonly Garden garden;
 readonly Timer animation=new Timer();
 readonly Stopwatch elapsed=Stopwatch.StartNew();
 readonly IslandPresence presence=new IslandPresence();
 readonly PillButton toggle=new PillButton(),open=new PillButton(),rest=new PillButton(),collapse=new PillButton(),pin=new PillButton();
 readonly PillButton[] durations=new PillButton[4];
 readonly ToolTip tips=new ToolTip();
 readonly Color mint=Color.FromArgb(167,227,188),muted=Color.FromArgb(137,148,153);
 bool expanded,dragging,moved,rendering,suspended,reward=true;
 Point downMouse,downWindow,anchor;
 Rectangle hotspot;
 double celebrationUntil,transitionStart,fromWidth=328,fromHeight=64,toWidth=328,toHeight=64,fromOpacity=1,toOpacity=1;
 const double Duration=.42;
 double testTime=-1;
 Point? testPointer;
 double Now { get { return testTime>=0?testTime:elapsed.Elapsed.TotalSeconds; } }
 protected override bool ShowWithoutActivation { get { return true; } }
 public FocusIsland(Garden owner) {
  garden=owner; Text="Focus Island"; AccessibleName="Focus Island";
  FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
  StartPosition=FormStartPosition.Manual; Size=new Size(328,64); Icon=Garden.LoadIcon();
  var area=Screen.FromControl(garden).WorkingArea; anchor=new Point(area.Left+area.Width/2,area.Top+16);
  Location=new Point(anchor.X-Width/2,anchor.Y); UpdateHotspot();
  BackColor=Color.FromArgb(16,20,24); ForeColor=Color.White; DoubleBuffered=true; KeyPreview=true; SetStyle(ControlStyles.ResizeRedraw,true);
  Setup(toggle,"Start focus",delegate { garden.ToggleFromIsland(); Touch(); });
  Setup(open,"Open garden",delegate { garden.ShowGarden(); });
  Setup(rest,"Break",delegate { garden.IslandBreak(); Touch(); });
  Setup(collapse,"−",delegate { SetExpanded(false); });
  Setup(pin,"Pin",delegate { presence.Pinned=!presence.Pinned; Touch(); });
  tips.SetToolTip(collapse,"Collapse · Esc"); tips.SetToolTip(pin,"Keep the island visible");
  for(int i=0;i<4;i++) { int minutes=new[]{15,25,50,1}[i]; durations[i]=new PillButton(); Setup(durations[i],minutes==1?"1m demo":minutes+"m",delegate { garden.IslandDuration(minutes); Touch(); }); durations[i].Font=new Font("Segoe UI",9); }
  MouseDown+=delegate(object s,MouseEventArgs e) { if(e.Button!=MouseButtons.Left) return; dragging=true; moved=false; downMouse=Cursor.Position; downWindow=Location; Capture=true; Touch(); };
  MouseMove+=delegate { if(!dragging) return; var delta=new Point(Cursor.Position.X-downMouse.X,Cursor.Position.Y-downMouse.Y); if(Math.Abs(delta.X)+Math.Abs(delta.Y)>5) moved=true; if(moved) { Location=new Point(downWindow.X+delta.X,downWindow.Y+delta.Y); anchor=new Point(Left+Width/2,Top); UpdateHotspot(); } };
  MouseUp+=delegate { if(!dragging) return; dragging=false; Capture=false; if(moved) ClampAnchor(); else SetExpanded(!expanded); Touch(); };
  MouseCaptureChanged+=delegate { if((Control.MouseButtons&MouseButtons.Left)==0) dragging=false; };
  KeyDown+=delegate(object s,KeyEventArgs e) { if(e.KeyCode==Keys.Escape) { SetExpanded(false); e.Handled=true; } if(e.KeyCode==Keys.Space && ActiveControl==null) { garden.ToggleFromIsland(); Touch(); e.Handled=true; } if(e.KeyCode==Keys.F2) garden.ShowGarden(); };
  Resize+=delegate { UpdateRegion(); LayoutButtons(); };
  animation.Interval=16; animation.Tick+=delegate { Frame(); }; animation.Start();
  presence.Wake(Now); UpdateRegion(); LayoutButtons(); RefreshButtons();
 }
 public void Reveal() { suspended=false; presence.Enabled=true; presence.Wake(Now); expanded=false; BeginTransition(328,64,1); Show(); animation.Start(); }
 public void Suspend() { suspended=true; presence.Enabled=false; Hide(); animation.Stop(); }
 void Touch() { presence.Wake(Now); RefreshButtons(); }
 static double Ease(double t) { t=Math.Max(0,Math.Min(1,t)); return 1-Math.Pow(1-t,5); }
 void BeginTransition(int width,int height,double opacity) { fromWidth=Width; fromHeight=Height; fromOpacity=Opacity; toWidth=width; toHeight=height; toOpacity=opacity; transitionStart=Now; foreach(Control c in Controls) c.Visible=false; animation.Interval=16; }
 void Frame() {
  if(rendering || suspended) return;
  double now=Now;
  Point pointer=testPointer??Cursor.Position;
  bool over=Visible && Bounds.Contains(pointer),busy=dragging || now<celebrationUntil;
  if(presence.Update(now,over,hotspot.Contains(pointer),busy)) {
   if(presence.Sleeping) { expanded=false; BeginTransition(300,56,.01); }
   else { expanded=false; BeginTransition(328,64,1); Show(); }
  }
  double fraction=(now-transitionStart)/Duration,t=Ease(fraction);
  int w=(int)Math.Round(fromWidth+(toWidth-fromWidth)*t),h=(int)Math.Round(fromHeight+(toHeight-fromHeight)*t);
  if(!dragging) SetBounds(anchor.X-w/2,anchor.Y,w,h);
  Opacity=Math.Max(.01,Math.Min(1,fromOpacity+(toOpacity-fromOpacity)*t));
  if(fraction>=1 && presence.Sleeping && Visible) { Hide(); animation.Interval=60; }
  else if(fraction>=1) { LayoutButtons(); animation.Interval=garden.State.Running || now<celebrationUntil?30:60; }
  RefreshButtons(); if(Visible) Invalidate();
 }
 void Setup(PillButton b,string text,Action action) { b.Text=text; b.Font=new Font("Segoe UI",10); b.Click+=delegate { action(); }; Controls.Add(b); }
 public void SetExpanded(bool value) { expanded=value; Touch(); BeginTransition(value?448:328,value?314:64,1); }
 void ClampAnchor() { var area=Screen.FromRectangle(Bounds).WorkingArea; anchor.X=Math.Max(area.Left+224,Math.Min(anchor.X,area.Right-224)); anchor.Y=Math.Max(area.Top,Math.Min(anchor.Y,area.Bottom-314)); UpdateHotspot(); Location=new Point(anchor.X-Width/2,anchor.Y); }
 void UpdateHotspot() { hotspot=new Rectangle(anchor.X-164,anchor.Y,328,64); }
 void UpdateRegion() { using(var path=Shapes.Round(new Rectangle(0,0,Width,Height),expanded?30:Height/2)) { var old=Region; Region=new Region(path); if(old!=null) old.Dispose(); } }
 void LayoutButtons() {
  bool show=expanded && Height>=310 && !presence.Sleeping; foreach(Control c in Controls) c.Visible=show;
  pin.SetBounds(318,19,58,28); collapse.SetBounds(384,19,38,28);
  for(int i=0;i<4;i++) durations[i].SetBounds(26+i*72,176,64,32);
  rest.SetBounds(322,176,100,32); toggle.SetBounds(26,246,238,44); open.SetBounds(274,246,148,44);
 }
 void RefreshButtons() {
  var s=garden.State;
  toggle.Text=s.Running?"Pause session":s.Remaining<s.Minutes*60?"Resume session":s.Rest?"Start break":"Start focus";
  toggle.Primary=true; pin.Selected=presence.Pinned; pin.Text=presence.Pinned?"Pinned":"Pin";
  for(int i=0;i<4;i++) { durations[i].Enabled=!s.Running; durations[i].Selected=s.Minutes==new[]{15,25,50,1}[i]; }
  rest.Enabled=!s.Running; rest.Text=s.Rest?"Focus":"Break";
  AccessibleDescription=garden.TimeText+" remaining. Click to expand. Drag to move. Hover over the previous position to reveal. F2 opens the garden.";
 }
 public void Celebrate(bool planted=true) { reward=planted; celebrationUntil=Now+8; if(suspended) return; presence.Wake(Now); expanded=true; BeginTransition(448,314,1); Show(); }
 protected override void OnPaint(PaintEventArgs e) {
  base.OnPaint(e); var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
  double now=Now; bool party=now<celebrationUntil; var s=garden.State;
  using(var gradient=new LinearGradientBrush(ClientRectangle,Color.FromArgb(26,32,37),Color.FromArgb(13,17,20),90f)) g.FillRectangle(gradient,ClientRectangle);
  using(var path=Shapes.Round(new Rectangle(1,1,Width-3,Height-3),expanded?29:Height/2-1)) using(var line=new Pen(Color.FromArgb(53,64,69))) g.DrawPath(line,path);
  float breathe=s.Running?(float)Math.Sin(now*2.3)*1.2f:0;
  using(var ring=new Pen(Color.FromArgb(48,61,58),2)) g.DrawEllipse(ring,20,17,30,30);
  using(var ring=new Pen(mint,2)) { ring.StartCap=LineCap.Round; ring.EndCap=LineCap.Round; g.DrawArc(ring,20,17,30,30,-90,(float)Math.Max(3,garden.Progress*360)); }
  using(var stem=new Pen(mint,1.8f)) { stem.StartCap=LineCap.Round; stem.EndCap=LineCap.Round; g.DrawLine(stem,35,39,35,29); g.DrawLine(stem,35,34,29,30-breathe); g.DrawLine(stem,35,30,41,26-breathe); }
  bool detail=expanded && Height>150;
  DrawText(g,party?(reward?"Session complete":"Break complete"):s.Rest?"Break":s.Running?"Focus session":"Focus Garden",detail?12:10,FontStyle.Bold,new Rectangle(64,detail?16:13,detail?230:150,24),Color.FromArgb(236,241,242));
  if(!detail) {
   DrawText(g,presence.Pinned?"Pinned to desktop":s.Running?"In progress":"Click to expand",8,FontStyle.Regular,new Rectangle(64,35,150,18),muted);
   DrawText(g,garden.TimeText,18,FontStyle.Regular,new Rectangle(229,16,90,33),mint);
  } else {
   DrawText(g,party?(reward?"A new plant in your garden":"Ready for another session"):s.Running?"IN PROGRESS":s.Remaining<s.Minutes*60?"PAUSED":"READY WHEN YOU ARE",8,FontStyle.Regular,new Rectangle(65,40,250,20),muted);
   DrawText(g,garden.TimeText,43,FontStyle.Regular,new Rectangle(23,67,245,74),Color.FromArgb(241,246,246));
   DrawText(g,party && reward?"+1 plant":s.Plants.Count+(s.Plants.Count==1?" plant":" plants"),12,FontStyle.Bold,new Rectangle(309,86,112,24),mint);
   DrawText(g,"in your garden",8,FontStyle.Regular,new Rectangle(309,112,112,20),muted);
   DrawText(g,String.IsNullOrWhiteSpace(s.Task)?"Set a task in your garden":s.Task,10,FontStyle.Regular,new Rectangle(27,140,394,25),Color.FromArgb(169,183,188));
   using(var track=new SolidBrush(Color.FromArgb(43,53,58))) g.FillRectangle(track,27,226,394,3);
   using(var fill=new SolidBrush(mint)) g.FillRectangle(fill,27,226,(float)(394*garden.Progress),3);
   if(party) for(int i=0;i<10;i++) { double angle=i*.68+now*.25; float x=357+(float)Math.Cos(angle)*56,y=101+(float)Math.Sin(angle)*31; using(var b=new SolidBrush(i%2==0?mint:Color.FromArgb(182,174,218))) g.FillEllipse(b,x,y,2,2); }
  }
 }
 static void DrawText(Graphics g,string value,int size,FontStyle style,Rectangle bounds,Color color) { using(var font=new Font("Segoe UI",size,style)) TextRenderer.DrawText(g,value,font,bounds,color,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter); }
 public void RenderTo(string file,bool full) { rendering=true; expanded=full; presence.Wake(Now); Size=full?new Size(448,314):new Size(328,64); Opacity=1; Location=new Point(-30000,-30000); Show(); LayoutButtons(); RefreshButtons(); using(var bmp=new Bitmap(Width,Height)) { DrawToBitmap(bmp,new Rectangle(0,0,Width,Height)); bmp.Save(file); } Hide(); rendering=false; }
 public void CheckLifecycle() {
  testTime=0; testPointer=Point.Empty; anchor=new Point(-30000,-30000); UpdateHotspot(); presence.Wake(0); Show(); BeginTransition(328,64,1); animation.Stop();
  Frame(); testTime=2; Frame(); testTime=2.5; Frame();
  if(Visible || !presence.Sleeping) throw new Exception("Island did not hide after its fade");
  testPointer=new Point(hotspot.X+100,hotspot.Y+30); testTime=3; Frame();
  if(Visible) throw new Exception("Island ignored hover dwell");
  testTime=3.2; Frame(); testTime=3.8; Frame();
  if(!Visible || Opacity<.99 || Width!=328 || Height!=64) throw new Exception("Island did not reveal correctly");
  SetExpanded(true); testTime=4.4; Frame();
  if(Width!=448 || Height!=314 || !toggle.Visible) throw new Exception("Island expansion failed");
  Suspend(); testTime=9; Frame(); if(Visible) throw new Exception("Suspended island reappeared");
  Reveal(); testTime=10; Frame(); if(!Visible || Width!=328) throw new Exception("Tray restore failed");
  Suspend(); testTime=-1; testPointer=null; suspended=false; presence.Enabled=true;
 }
 protected override void Dispose(bool disposing) { if(disposing) { animation.Dispose(); tips.Dispose(); } base.Dispose(disposing); }
}

public static class Shapes {
 public static GraphicsPath Round(Rectangle r,int radius) { int d=Math.Max(2,Math.Min(radius*2,Math.Min(r.Width,r.Height))); var p=new GraphicsPath(); p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p; }
}

public class PillButton : Button {
 public bool Primary,Selected,UseControlColors;
 float hover; bool over;
 readonly Timer motion=new Timer();
 public PillButton() {
  FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0; Cursor=Cursors.Hand; DoubleBuffered=true; SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
  motion.Interval=16; motion.Tick+=delegate { float target=over?1:0; hover+=(target-hover)*.22f; if(Math.Abs(target-hover)<.01f) { hover=target; motion.Stop(); } Invalidate(); };
  MouseEnter+=delegate { over=true; motion.Start(); }; MouseLeave+=delegate { over=false; motion.Start(); };
 }
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; Color basis=UseControlColors?BackColor:Primary?Color.FromArgb(167,227,188):Selected?Color.FromArgb(49,75,65):Color.FromArgb(34,42,47);
  Color fill=Color.FromArgb(Math.Min(255,basis.R+(int)(hover*12)),Math.Min(255,basis.G+(int)(hover*12)),Math.Min(255,basis.B+(int)(hover*12)));
  using(var path=Shapes.Round(new Rectangle(0,0,Width-1,Height-1),12)) using(var b=new SolidBrush(fill)) g.FillPath(b,path);
  Color ink=!Enabled?Color.FromArgb(100,114,119):UseControlColors?ForeColor:Primary?Color.FromArgb(18,41,29):Color.FromArgb(224,233,236);
  TextRenderer.DrawText(g,Text,Font,ClientRectangle,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
  if(Focused) using(var p=new Pen(Color.FromArgb(132,159,148))) using(var path=Shapes.Round(new Rectangle(3,3,Width-7,Height-7),9)) g.DrawPath(p,path);
 }
 protected override void Dispose(bool disposing) { if(disposing) motion.Dispose(); base.Dispose(disposing); }
}
