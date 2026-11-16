using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// A separate native window. No browser, screen capture, or global input hooks.
public sealed class FocusIsland : Form {
 readonly Garden garden;
 readonly Timer animation=new Timer();
 readonly Button toggle=new Button(), open=new Button(), rest=new Button(), collapse=new Button(), quit=new Button();
 readonly Button[] durations=new Button[4];
 readonly ToolTip tips=new ToolTip();
 bool expanded, dragging, moved;
 Point downMouse, downWindow;
 DateTime celebration=DateTime.MinValue;
 float phase;
 int targetWidth=318,targetHeight=64;
 readonly Color mint=Color.FromArgb(184,225,148), faint=Color.FromArgb(137,155,144);
 public FocusIsland(Garden owner) {
  garden=owner; Text="Focus Island"; AccessibleName="Focus Island — плавающий таймер";
  FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true;
  StartPosition=FormStartPosition.Manual; Size=new Size(318,64);
  var area=Screen.FromControl(garden).WorkingArea; Location=new Point(area.Left+(area.Width-Width)/2,area.Top+16);
  BackColor=Color.FromArgb(13,19,17); ForeColor=Color.White; DoubleBuffered=true; KeyPreview=true;
  SetStyle(ControlStyles.ResizeRedraw,true);
  SetupButton(toggle,"Начать фокус",delegate { garden.ToggleFromIsland(); RefreshButtons(); });
  SetupButton(open,"Открыть сад ↗",delegate { garden.ShowGarden(); });
  SetupButton(rest,"Передышка",delegate { garden.IslandBreak(); RefreshButtons(); });
  SetupButton(collapse,"−",delegate { SetExpanded(false); });
  SetupButton(quit,"×",delegate { garden.ExitFromIsland(); });
  tips.SetToolTip(collapse,"Свернуть · Esc"); tips.SetToolTip(quit,"Завершить приложение и сохранить сад");
  for(int i=0;i<4;i++) { int minutes=new[]{15,25,50,1}[i]; durations[i]=new Button(); SetupButton(durations[i],minutes==1?"1 мин":minutes+" мин",delegate { garden.IslandDuration(minutes); RefreshButtons(); }); durations[i].Font=new Font("Segoe UI",8); }
  MouseDown+=delegate(object s,MouseEventArgs e) { if(e.Button!=MouseButtons.Left) return; dragging=true; moved=false; downMouse=Cursor.Position; downWindow=Location; Capture=true; };
  MouseMove+=delegate { if(!dragging) return; Point delta=new Point(Cursor.Position.X-downMouse.X,Cursor.Position.Y-downMouse.Y); if(Math.Abs(delta.X)+Math.Abs(delta.Y)>5) moved=true; if(moved) Location=new Point(downWindow.X+delta.X,downWindow.Y+delta.Y); };
  MouseUp+=delegate { if(!dragging) return; dragging=false; Capture=false; if(moved) ClampToScreen(); else SetExpanded(!expanded); };
  MouseCaptureChanged+=delegate { if((Control.MouseButtons&MouseButtons.Left)==0) dragging=false; };
  KeyDown+=delegate(object s,KeyEventArgs e) { if(e.KeyCode==Keys.Escape) { SetExpanded(false); e.Handled=true; } if(e.KeyCode==Keys.Space && !ContainsFocusedButton()) { garden.ToggleFromIsland(); e.Handled=true; } if(e.KeyCode==Keys.F2) garden.ShowGarden(); };
  Resize+=delegate { UpdateRegion(); LayoutButtons(); };
  animation.Interval=16; animation.Tick+=delegate {
   phase+=.055f;
   if(Width!=targetWidth || Height!=targetHeight) {
    int w=Approach(Width,targetWidth),h=Approach(Height,targetHeight),center=Left+Width/2;
    SetBounds(center-w/2,Top,w,h); if(w==targetWidth && h==targetHeight) ClampToScreen();
   }
   RefreshButtons(); Invalidate();
  };
  VisibleChanged+=delegate { if(Visible) animation.Start(); else animation.Stop(); };
  UpdateRegion(); LayoutButtons(); RefreshButtons();
 }
 bool ContainsFocusedButton() { foreach(Control c in Controls) if(c.Focused) return true; return false; }
 static int Approach(int value,int target) { int diff=target-value; return Math.Abs(diff)<3?target:value+(int)Math.Round(diff*.24); }
 void SetupButton(Button b,string text,Action action) {
  b.Text=text; b.FlatStyle=FlatStyle.Flat; b.FlatAppearance.BorderSize=0; b.BackColor=Color.FromArgb(36,49,41); b.ForeColor=Color.FromArgb(225,236,218); b.Font=new Font("Segoe UI",10); b.Cursor=Cursors.Hand; b.Click+=delegate { action(); }; Controls.Add(b);
 }
 public void SetExpanded(bool value) { expanded=value; targetWidth=value?460:318; targetHeight=value?284:64; LayoutButtons(); Invalidate(); }
 void ClampToScreen() { Rectangle area=Screen.FromRectangle(Bounds).WorkingArea; Left=Math.Max(area.Left,Math.Min(Left,area.Right-Width)); Top=Math.Max(area.Top,Math.Min(Top,area.Bottom-Height)); }
 void UpdateRegion() { using(var path=RoundPath(ClientRectangle,Math.Min(32,Height/2))) { var old=Region; Region=new Region(path); if(old!=null) old.Dispose(); } }
 static GraphicsPath RoundPath(Rectangle r,int radius) { var p=new GraphicsPath(); int d=radius*2; p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p; }
 void LayoutButtons() {
  bool visible=expanded && Height>=270;
  foreach(Control c in Controls) c.Visible=visible;
  toggle.SetBounds(24,218,238,42); open.SetBounds(274,218,162,42);
  rest.SetBounds(312,153,124,32); collapse.SetBounds(365,17,32,30); quit.SetBounds(403,17,32,30);
  for(int i=0;i<4;i++) durations[i].SetBounds(24+i*70,153,62,32);
 }
 void RefreshButtons() {
  var s=garden.State; string text=s.Running?"Ⅱ  Пауза":s.Remaining<s.Minutes*60?"▶  Продолжить":s.Rest?"▶  Отдохнуть":"▶  Начать фокус";
  if(toggle.Text!=text) toggle.Text=text;
  toggle.BackColor=mint; toggle.ForeColor=Color.FromArgb(20,36,23);
  foreach(var b in durations) b.Enabled=!s.Running;
  rest.Enabled=!s.Running; rest.Text=s.Rest?"К фокусу":"Передышка";
  AccessibleDescription=(s.Running?"Таймер работает. ":"Таймер на паузе. ")+garden.TimeText+". Нажми для раскрытия; перетащи для перемещения; F2 — открыть сад.";
 }
 public void Celebrate() { celebration=DateTime.UtcNow.AddSeconds(9); if(Visible) SetExpanded(true); }
 protected override void OnPaint(PaintEventArgs e) {
  base.OnPaint(e); Graphics g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
  bool party=celebration>DateTime.UtcNow; var s=garden.State;
  using(var line=new Pen(Color.FromArgb(48,65,52),1)) using(var path=RoundPath(new Rectangle(0,0,Width-1,Height-1),Math.Min(32,(Height-1)/2))) g.DrawPath(line,path);
  // Progress surrounds a tiny seedling, breathing only during a running session.
  int cy=32; float breathe=s.Running?(float)(Math.Sin(phase)*1.8):0;
  using(var ring=new Pen(Color.FromArgb(40,57,45),3)) g.DrawEllipse(ring,20,17,30,30);
  using(var ring=new Pen(mint,3)) g.DrawArc(ring,20,17,30,30,-90,(float)Math.Max(4,garden.Progress*360));
  using(var stem=new Pen(mint,2)) { g.DrawLine(stem,35,cy+7,35,cy-4); g.DrawLine(stem,35,cy+1,29,cy-3-breathe); g.DrawLine(stem,35,cy-3,41,cy-7-breathe); }
  string heading=party?"Новый росток!":s.Rest?"Время выдохнуть":s.Running?"Тихо. Мы растём.":expanded?"Твой маленький остров":"Твой остров";
  DrawText(g,heading,expanded?12:10,FontStyle.Bold,new Rectangle(64,expanded?15:13,expanded?280:154,23),party?mint:Color.FromArgb(231,239,226));
  if(!expanded || Height<100) {
   DrawText(g,s.Running?"фокус включён":s.Rest?"передышка":"нажми, чтобы раскрыть",8,FontStyle.Regular,new Rectangle(64,35,158,18),faint);
   DrawText(g,garden.TimeText,17,FontStyle.Bold,new Rectangle(222,17,87,32),mint);
  } else {
   DrawText(g,party?"Ещё одна маленькая победа":s.Rest?"Отдых — тоже часть роста":"FOCUS GARDEN / ISLAND",8,FontStyle.Regular,new Rectangle(65,39,280,20),faint);
   DrawText(g,garden.TimeText,42,FontStyle.Regular,new Rectangle(22,61,230,72),mint);
   DrawText(g,party?"+1 растение":s.Plants.Count+" в твоём саду",10,FontStyle.Regular,new Rectangle(275,81,160,23),mint);
   DrawText(g,s.Running?"РАСТЁМ В СВОЁМ ТЕМПЕ":"МОЖНО НЕ СПЕШИТЬ",7,FontStyle.Regular,new Rectangle(275,108,170,20),faint);
   DrawText(g,String.IsNullOrWhiteSpace(s.Task)?"Одна сессия — один новый росток":s.Task,9,FontStyle.Regular,new Rectangle(25,128,410,23),Color.FromArgb(177,194,177));
   using(var track=new SolidBrush(Color.FromArgb(40,57,45))) g.FillRectangle(track,25,199,410,2);
   using(var fill=new SolidBrush(mint)) g.FillRectangle(fill,25,199,(int)(410*garden.Progress),2);
   if(party) for(int i=0;i<12;i++) { float angle=i*.53f+phase*.3f; float x=350+(float)Math.Cos(angle)*62,y=85+(float)Math.Sin(angle)*30; using(var b=new SolidBrush(i%2==0?mint:Color.FromArgb(210,168,190))) g.FillEllipse(b,x,y,3,3); }
  }
 }
 static void DrawText(Graphics g,string value,int size,FontStyle style,Rectangle bounds,Color color) { using(var font=new Font("Segoe UI",size,style)) TextRenderer.DrawText(g,value,font,bounds,color,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter); }
 public void RenderTo(string file,bool full) { expanded=full; targetWidth=full?460:318; targetHeight=full?284:64; Size=new Size(targetWidth,targetHeight); Location=new Point(-30000,-30000); Show(); LayoutButtons(); RefreshButtons(); using(var bmp=new Bitmap(Width,Height)) { DrawToBitmap(bmp,new Rectangle(0,0,Width,Height)); bmp.Save(file); } Hide(); }
 protected override void Dispose(bool disposing) { if(disposing) { animation.Stop(); animation.Dispose(); tips.Dispose(); } base.Dispose(disposing); }
}
