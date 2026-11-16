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
 readonly TextBox taskEditor=new CueTextBox { Cue="Name this session" };
 readonly PillButton queueButton=new PillButton();
 readonly Color mint=Color.FromArgb(167,227,188),muted=Color.FromArgb(137,148,153);
 bool expanded,dragging,moved,rendering,suspended,reward=true,choosingDuration;
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
  if(garden.State.HasIslandPosition) { anchor=new Point(garden.State.IslandX,garden.State.IslandY); ClampAnchor(); }
  presence.Pinned=garden.State.IslandPinned;
  Location=new Point(anchor.X-Width/2,anchor.Y); UpdateHotspot();
  BackColor=Color.FromArgb(16,20,24); ForeColor=Color.White; DoubleBuffered=true; KeyPreview=true; SetStyle(ControlStyles.ResizeRedraw,true);
  Setup(toggle,"Start focus",delegate { garden.ToggleFromIsland(); Touch(); });
  Setup(open,"Open garden",delegate { garden.ShowGarden(); });
  Setup(rest,"Break",delegate { garden.IslandBreak(); Touch(); });
  Setup(collapse,"−",delegate { SetExpanded(false); });
  Setup(pin,"Pin",delegate { presence.Pinned=!presence.Pinned; garden.RememberIsland(anchor,presence.Pinned); Touch(); });
  tips.SetToolTip(collapse,"Collapse · Esc"); tips.SetToolTip(pin,"Keep the island visible");
  for(int i=0;i<4;i++) { int index=i,minutes=new[]{15,25,50,1}[i]; durations[i]=new PillButton(); Setup(durations[i],minutes+"m",delegate { if(index==3) { choosingDuration=true; try { garden.ChooseDuration(this); } finally { choosingDuration=false; } } else garden.IslandDuration(minutes); Touch(); }); durations[i].Font=new Font("Segoe UI",9); }
  tips.SetToolTip(durations[3],"Choose a custom duration · 1 to 120 minutes");
  taskEditor.BorderStyle=BorderStyle.None; taskEditor.BackColor=Color.FromArgb(30,38,43); taskEditor.ForeColor=Color.FromArgb(214,226,230); taskEditor.Font=new Font("Segoe UI",10); taskEditor.MaxLength=120; taskEditor.AccessibleName="Session task"; taskEditor.Text=garden.State.Task??""; Controls.Add(taskEditor);
  taskEditor.TextChanged+=delegate { if(taskEditor.Focused) garden.SetIslandTask(taskEditor.Text); };
  tips.SetToolTip(taskEditor,"Name your task · Ctrl+E to edit · Ctrl+Enter to start or pause");
  Setup(queueButton,"▾",ShowQueueMenu); queueButton.AccessibleName="Choose a queued task"; tips.SetToolTip(queueButton,"Choose a task from your queue");
  MouseDown+=delegate(object s,MouseEventArgs e) { if(e.Button!=MouseButtons.Left) return; dragging=true; moved=false; downMouse=Cursor.Position; downWindow=Location; Capture=true; Touch(); };
  MouseMove+=delegate { if((Control.MouseButtons&MouseButtons.Left)==0) dragging=false; if(!dragging) return; var delta=new Point(Cursor.Position.X-downMouse.X,Cursor.Position.Y-downMouse.Y); if(Math.Abs(delta.X)+Math.Abs(delta.Y)>5) moved=true; if(moved) { Location=new Point(downWindow.X+delta.X,downWindow.Y+delta.Y); anchor=new Point(Left+Width/2,Top); UpdateHotspot(); } };
  MouseUp+=delegate { if(!dragging) return; dragging=false; Capture=false; if(moved) { ClampAnchor(); garden.RememberIsland(anchor,presence.Pinned); } else SetExpanded(!expanded); Touch(); };
  MouseCaptureChanged+=delegate { if((Control.MouseButtons&MouseButtons.Left)==0) dragging=false; };
  KeyDown+=delegate(object s,KeyEventArgs e) { if(e.KeyCode==Keys.Escape) { SetExpanded(false); e.Handled=true; } if(e.KeyCode==Keys.Space && ActiveControl==null) { garden.ToggleFromIsland(); Touch(); e.Handled=true; } if(e.KeyCode==Keys.F2) garden.ShowGarden(); };
  KeyDown+=delegate(object s,KeyEventArgs e) { if(e.Control && e.KeyCode==Keys.Enter) { garden.ToggleFromIsland(); Touch(); e.SuppressKeyPress=true; } if(e.Control && e.KeyCode==Keys.E) { SetExpanded(true); if(garden.State.ReducedMotion) { taskEditor.Focus(); taskEditor.SelectAll(); } else { var focusTimer=new Timer { Interval=450 }; focusTimer.Tick+=delegate { focusTimer.Dispose(); if(!IsDisposed && Visible && expanded) { taskEditor.Focus(); taskEditor.SelectAll(); } }; focusTimer.Start(); } e.SuppressKeyPress=true; } };
  Resize+=delegate { UpdateRegion(); LayoutButtons(); };
  animation.Interval=16; animation.Tick+=delegate { Frame(); }; animation.Start();
  presence.Wake(Now); UpdateRegion(); LayoutButtons(); RefreshButtons(); ApplyMotion();
 }
 public void Reveal() { suspended=false; presence.Enabled=true; presence.Wake(Now); expanded=false; if(testTime<0 && !rendering) ClampAnchor(); BeginTransition(328,64,1); Show(); animation.Start(); }
 public void Suspend() { suspended=true; presence.Enabled=false; Hide(); animation.Stop(); }
 void Touch() { presence.Wake(Now); RefreshButtons(); }
 static double Ease(double t) { t=Math.Max(0,Math.Min(1,t)); return 1-Math.Pow(1-t,5); }
 void BeginTransition(int width,int height,double opacity) { fromWidth=Width; fromHeight=Height; fromOpacity=Opacity; toWidth=width; toHeight=height; toOpacity=opacity; transitionStart=Now; foreach(Control c in Controls) c.Visible=false; animation.Interval=16; if(garden.State.ReducedMotion) { SetBounds(anchor.X-width/2,anchor.Y,width,height); Opacity=Math.Max(.01,opacity); LayoutButtons(); } }
 void Frame() {
  if(rendering || suspended) return;
  double now=Now;
  Point pointer=testPointer??Cursor.Position;
  bool over=Visible && Bounds.Contains(pointer),busy=dragging || choosingDuration || taskEditor.Focused || now<celebrationUntil;
  if(presence.Update(now,over,hotspot.Contains(pointer),busy)) {
   if(presence.Sleeping) { expanded=false; BeginTransition(300,56,.01); }
   else { expanded=false; BeginTransition(328,64,1); Show(); }
  }
  if(presence.Sleeping && !Visible) { animation.Interval=80; return; }
  double fraction=garden.State.ReducedMotion?1:(now-transitionStart)/Duration,t=Ease(fraction);
  int w=(int)Math.Round(fromWidth+(toWidth-fromWidth)*t),h=(int)Math.Round(fromHeight+(toHeight-fromHeight)*t);
  if(!dragging) SetBounds(anchor.X-w/2,anchor.Y,w,h);
  Opacity=Math.Max(.01,Math.Min(1,fromOpacity+(toOpacity-fromOpacity)*t));
  if(fraction>=1 && presence.Sleeping && Visible) { Hide(); animation.Interval=60; }
  else if(fraction>=1) { LayoutButtons(); animation.Interval=garden.State.Running || now<celebrationUntil?30:60; }
  RefreshButtons(); if(Visible) Invalidate();
 }
 void Setup(PillButton b,string text,Action action) { b.Text=text; b.Font=new Font("Segoe UI",10); b.Click+=delegate { action(); }; Controls.Add(b); }
 public void SetExpanded(bool value) { expanded=value; Touch(); BeginTransition(value?448:328,value?314:64,1); }
 void ClampAnchor() { var area=Screen.FromPoint(anchor).WorkingArea; anchor.X=Math.Max(area.Left+224,Math.Min(anchor.X,area.Right-224)); anchor.Y=Math.Max(area.Top,Math.Min(anchor.Y,area.Bottom-314)); UpdateHotspot(); Location=new Point(anchor.X-Width/2,anchor.Y); }
 void UpdateHotspot() { hotspot=new Rectangle(anchor.X-164,anchor.Y,328,64); }
 void UpdateRegion() { using(var path=Shapes.Round(new Rectangle(0,0,Width,Height),expanded?30:Height/2)) { var old=Region; Region=new Region(path); if(old!=null) old.Dispose(); } }
 void LayoutButtons() {
  bool show=expanded && Height>=310 && !presence.Sleeping; foreach(Control c in Controls) c.Visible=show;
  pin.SetBounds(318,19,58,28); collapse.SetBounds(384,19,38,28);
  taskEditor.SetBounds(38,145,330,21); queueButton.SetBounds(382,142,30,24);
  for(int i=0;i<4;i++) durations[i].SetBounds(26+i*72,176,64,32);
  rest.SetBounds(322,176,100,32); toggle.SetBounds(26,246,238,44); open.SetBounds(274,246,148,44);
 }
 void RefreshButtons() {
  var s=garden.State;
  toggle.Text=s.Running?"Pause session":s.Remaining<s.Minutes*60?"Resume session":s.Rest?"Start break":"Start focus";
  toggle.Primary=true; pin.Selected=presence.Pinned; pin.Text=presence.Pinned?"Pinned":"Pin";
  for(int i=0;i<4;i++) { durations[i].Enabled=!s.Running; durations[i].Selected=s.Minutes==(i==3?s.CustomMinutes:new[]{15,25,50,1}[i]); }
  durations[3].Text=s.CustomMinutes+"m +";
  if(!taskEditor.Focused && taskEditor.Text!=(s.Task??"")) taskEditor.Text=s.Task??"";
  rest.Enabled=!s.Running; rest.Text=s.Rest?"Focus":"Break";
  queueButton.Enabled=!s.Running;
  AccessibleDescription=garden.TimeText+" remaining. Click to expand. Drag to move. Hover over the previous position to reveal. F2 opens the garden.";
 }
 public void ApplyMotion() { foreach(Control c in Controls) { var button=c as PillButton; if(button!=null) button.ReducedMotion=garden.State.ReducedMotion; } }
 void ShowQueueMenu() {
  choosingDuration=true;
  var menu=new ContextMenuStrip { BackColor=Color.FromArgb(26,34,39),ForeColor=Color.FromArgb(228,238,240),ShowImageMargin=false,Renderer=new ToolStripProfessionalRenderer(new IslandMenuColors()) };
  int pending=0;
  foreach(var queued in garden.State.Tasks) if(!queued.Done && !queued.Archived) { var selected=queued; string label=queued.Title.Replace("\r"," ").Replace("\n"," "); if(label.Length>60) label=label.Substring(0,57)+"…"; var item=menu.Items.Add(label.Replace("&","&&")); item.ToolTipText=queued.Title; item.Click+=delegate { garden.SelectQueuedTask(selected.Id); Touch(); }; pending++; if(pending==20) break; }
  if(pending==0) menu.Items.Add("No pending tasks").Enabled=false;
  menu.Items.Add(new ToolStripSeparator());
  if(!String.IsNullOrEmpty(garden.State.ActiveTaskId)) { string id=garden.State.ActiveTaskId; menu.Items.Add("Mark current task done",null,delegate { garden.CompleteQueuedTask(id); Touch(); }); }
  menu.Items.Add("Manage tasks…",null,delegate { garden.ShowTasks(); });
  menu.Closed+=delegate { choosingDuration=false; menu.Dispose(); Touch(); };
  menu.Show(this,new Point(26,171));
 }
 public void Celebrate(bool planted=true) { reward=planted; celebrationUntil=Now+8; if(suspended) return; presence.Wake(Now); expanded=true; BeginTransition(448,314,1); Show(); }
 protected override void OnPaint(PaintEventArgs e) {
  base.OnPaint(e); var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
  double now=Now; bool party=now<celebrationUntil; var s=garden.State;
  using(var gradient=new LinearGradientBrush(ClientRectangle,Color.FromArgb(26,32,37),Color.FromArgb(13,17,20),90f)) g.FillRectangle(gradient,ClientRectangle);
  using(var path=Shapes.Round(new Rectangle(1,1,Width-3,Height-3),expanded?29:Height/2-1)) using(var line=new Pen(Color.FromArgb(53,64,69))) g.DrawPath(line,path);
  float breathe=s.Running && !s.ReducedMotion?(float)Math.Sin(now*2.3)*1.2f:0;
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
   using(var field=new SolidBrush(Color.FromArgb(30,38,43))) using(var shape=Shapes.Round(new Rectangle(26,139,396,30),9)) g.FillPath(field,shape);
   using(var track=new SolidBrush(Color.FromArgb(43,53,58))) g.FillRectangle(track,27,226,394,3);
   using(var fill=new SolidBrush(mint)) g.FillRectangle(fill,27,226,(float)(394*garden.Progress),3);
   DrawText(g,"TODAY  "+garden.TodayMinutes+" / "+s.DailyGoal+"m",7,FontStyle.Regular,new Rectangle(27,210,210,14),muted);
   DrawText(g,s.SelectedKind<0?"MIXED SEEDS":Garden.PlantName(s.SelectedKind).ToUpperInvariant(),7,FontStyle.Regular,new Rectangle(305,210,117,14),mint);
   if(party && !s.ReducedMotion) {
    double age=8-(celebrationUntil-now);
    if(age<3) for(int i=0;i<18;i++) { double angle=i*2.39996,travel=10+age*(18+i%5*4); float x=356+(float)(Math.Cos(angle)*travel),y=101+(float)(Math.Sin(angle)*travel-age*20+age*age*8); Color color=i%3==0?Color.FromArgb(182,174,218):mint; using(var b=new SolidBrush(Color.FromArgb((int)(220*(1-age/3)),color))) g.FillEllipse(b,x,y,2+i%2,2+i%2); }
   }
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
 public void RenderMotionFrames(string folder) {
  System.IO.Directory.CreateDirectory(folder); animation.Stop(); anchor=new Point(-30000,-30000); UpdateHotspot(); testPointer=new Point(anchor.X,anchor.Y+30); testTime=0; presence.Pinned=true; presence.Wake(0); Show(); BeginTransition(328,64,1);
  for(int i=0;i<120;i++) {
   testTime=i/30.0;
   if(i==20) SetExpanded(true);
   if(i==58) { celebrationUntil=Now+8; reward=true; }
   if(i==90) { celebrationUntil=0; SetExpanded(false); }
   Frame();
   using(var form=new Bitmap(Width,Height)) using(var frame=new Bitmap(500,360)) using(var g=Graphics.FromImage(frame)) { g.Clear(Color.FromArgb(16,20,24)); DrawToBitmap(form,new Rectangle(Point.Empty,Size)); g.DrawImageUnscaled(form,(500-Width)/2,16); frame.Save(System.IO.Path.Combine(folder,"frame-"+i.ToString("000")+".png")); }
  }
  Suspend();
 }
 protected override void Dispose(bool disposing) { if(disposing) { animation.Dispose(); tips.Dispose(); } base.Dispose(disposing); }
}

public sealed class IslandMenuColors : ProfessionalColorTable {
 public override Color ToolStripDropDownBackground { get { return Color.FromArgb(26,34,39); } }
 public override Color MenuBorder { get { return Color.FromArgb(60,77,81); } }
 public override Color MenuItemSelected { get { return Color.FromArgb(48,69,60); } }
 public override Color MenuItemBorder { get { return Color.FromArgb(85,116,98); } }
 public override Color MenuItemSelectedGradientBegin { get { return MenuItemSelected; } }
 public override Color MenuItemSelectedGradientEnd { get { return MenuItemSelected; } }
}

public static class Shapes {
 public static GraphicsPath Round(Rectangle r,int radius) { int d=Math.Max(2,Math.Min(radius*2,Math.Min(r.Width,r.Height))); var p=new GraphicsPath(); p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p; }
}

public class PillButton : Button {
 public bool Primary,Selected,UseControlColors,Destructive,ReducedMotion;
 float hover; bool over;
 readonly Timer motion=new Timer();
 public PillButton() {
  FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0; Cursor=Cursors.Hand; DoubleBuffered=true; SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
  motion.Interval=16; motion.Tick+=delegate { float target=over?1:0; hover+=(target-hover)*.22f; if(Math.Abs(target-hover)<.01f) { hover=target; motion.Stop(); } Invalidate(); };
  MouseEnter+=delegate { over=true; if(ReducedMotion) { hover=1; Invalidate(); } else motion.Start(); }; MouseLeave+=delegate { over=false; if(ReducedMotion) { hover=0; Invalidate(); } else motion.Start(); };
 }
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; Color basis=UseControlColors?BackColor:Primary?Color.FromArgb(167,227,188):Selected?Color.FromArgb(49,75,65):Color.FromArgb(34,42,47);
  Color fill=Color.FromArgb(Math.Min(255,basis.R+(int)(hover*12)),Math.Min(255,basis.G+(int)(hover*12)),Math.Min(255,basis.B+(int)(hover*12)));
  if(Destructive && hover>0) fill=Color.FromArgb((int)(basis.R+(185-basis.R)*hover),(int)(basis.G+(62-basis.G)*hover),(int)(basis.B+(69-basis.B)*hover));
  using(var path=Shapes.Round(new Rectangle(0,0,Width-1,Height-1),12)) using(var b=new SolidBrush(fill)) g.FillPath(b,path);
  Color ink=!Enabled?Color.FromArgb(100,114,119):UseControlColors?ForeColor:Primary?Color.FromArgb(18,41,29):Color.FromArgb(224,233,236);
  TextRenderer.DrawText(g,Text,Font,ClientRectangle,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
  if(Focused) using(var p=new Pen(Color.FromArgb(132,159,148))) using(var path=Shapes.Round(new Rectangle(3,3,Width-7,Height-7),9)) g.DrawPath(p,path);
 }
 protected override void Dispose(bool disposing) { if(disposing) motion.Dispose(); base.Dispose(disposing); }
}

public sealed class DurationPicker : Form {
 readonly TextBox minutes=new TextBox();
 public int Minutes { get { int value; return TryParseMinutes(minutes.Text,out value)?value:35; } }
 public static bool TryParseMinutes(string text,out int selected) { return int.TryParse(text,out selected) && selected>=1 && selected<=120; }
 public DurationPicker(int initial,bool reducedMotion=false) {
  Text="Custom duration"; Icon=Garden.LoadIcon(); FormBorderStyle=FormBorderStyle.None; StartPosition=FormStartPosition.CenterParent; ShowInTaskbar=false; TopMost=true; ClientSize=new Size(320,210); BackColor=Color.FromArgb(22,29,34); ForeColor=Color.FromArgb(230,240,241); Font=new Font("Segoe UI",10);
  Controls.Add(new Label { Text="Custom duration",Location=new Point(24,20),Size=new Size(272,28),Font=new Font("Segoe UI",15,FontStyle.Bold) });
  var help=new Label { Text="Minutes · 1 to 120",Location=new Point(26,58),Size=new Size(270,23),ForeColor=Color.FromArgb(149,168,175) }; Controls.Add(help);
  minutes.Text=Math.Max(1,Math.Min(120,initial)).ToString(); minutes.MaxLength=3; minutes.SetBounds(26,91,174,36); minutes.Font=new Font("Segoe UI",18); minutes.BackColor=Color.FromArgb(34,44,50); minutes.ForeColor=ForeColor; minutes.BorderStyle=BorderStyle.FixedSingle; minutes.AccessibleName="Session duration in minutes"; Controls.Add(minutes);
  var less=new PillButton { Text="−",ReducedMotion=reducedMotion,Location=new Point(212,91),Size=new Size(36,38),AccessibleName="Decrease minutes" }; less.Click+=delegate { minutes.Text=Math.Max(1,Minutes-1).ToString(); }; Controls.Add(less);
  var more=new PillButton { Text="+",ReducedMotion=reducedMotion,Location=new Point(258,91),Size=new Size(36,38),AccessibleName="Increase minutes" }; more.Click+=delegate { minutes.Text=Math.Min(120,Minutes+1).ToString(); }; Controls.Add(more);
  var cancel=new PillButton { Text="Cancel",ReducedMotion=reducedMotion,Location=new Point(26,153),Size=new Size(125,36),DialogResult=DialogResult.Cancel }; Controls.Add(cancel);
  var apply=new PillButton { Text="Set duration",ReducedMotion=reducedMotion,Primary=true,Location=new Point(161,153),Size=new Size(133,36) }; apply.Click+=delegate { int selected; if(!TryParseMinutes(minutes.Text,out selected)) { help.Text="Enter a whole number from 1 to 120."; return; } minutes.Text=selected.ToString(); DialogResult=DialogResult.OK; }; Controls.Add(apply);
  AcceptButton=apply; CancelButton=cancel;
  using(var shape=Shapes.Round(ClientRectangle,20)) Region=new Region(shape);
 }
}
