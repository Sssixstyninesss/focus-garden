using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

[DataContract] public class Plant {
 [DataMember] public string Task;
 [DataMember] public int Minutes;
 [DataMember] public DateTime Date;
 [DataMember] public int Kind;
}
[DataContract] public class SaveData {
 [DataMember] public bool Dark = true;
 [DataMember] public List<Plant> Plants = new List<Plant>();
 [DataMember] public int Minutes = 25;
 [DataMember] public double Remaining = 1500;
 [DataMember] public bool Running;
 [DataMember] public bool Rest;
 [DataMember] public DateTime Deadline = DateTime.UtcNow;
 [DataMember] public string Task = "";
}
public class Garden : Form {
 SaveData data = new SaveData();
 string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "garden.json");
 Label clock = new Label(), hint = new Label(), stats = new Label(), title = new Label(), tagline = new Label(), taskLabel = new Label(), gardenLabel = new Label();
 TextBox task = new TextBox(); ListBox history = new ListBox();
 Button start, reset, theme, mode, export, import;
 List<Button> durations = new List<Button>();
 Panel timerPanel = new Panel(), gardenPanel = new Panel();
 Canvas canvas = new Canvas();
 System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
 bool loading = true, sound = true; Button soundButton;
 Color bg, panel, ink, muted, border, accent;
 [STAThread] public static void Main(string[] args) {
  if (args.Contains("--self-test")) { SelfTest(); return; }
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  using (var mutex = new System.Threading.Mutex(true,"Local\\FocusGardenDesktop",out single)) { if (!single) { MessageBox.Show("Focus Garden уже запущен."); return; } Application.Run(new Garden()); }
 }
 static bool single;
 static void SelfTest() {
  var d = new SaveData(); d.Plants.Add(new Plant { Task = "Тест", Minutes = 25, Date = DateTime.UtcNow, Kind = 2 });
  using(var ms = new MemoryStream()) { Serializer().WriteObject(ms,d); ms.Position=0; var copy=(SaveData)Serializer().ReadObject(ms); Validate(copy); if(copy.Plants[0].Task!="Тест" || !copy.Dark) throw new Exception("Serialization failed"); }
  if (Format(65)!="01:05" || Seconds(DateTime.UtcNow.AddSeconds(-2))!=0) throw new Exception("Timer failed");
  Console.WriteLine("PASS: serialization, validation, time formatting, elapsed deadline");
 }
 static DataContractJsonSerializer Serializer() { return new DataContractJsonSerializer(typeof(SaveData)); }
 static void Validate(SaveData d) {
  if(d==null || d.Plants==null || d.Plants.Count>10000 || d.Minutes<1 || d.Minutes>120 || double.IsNaN(d.Remaining) || double.IsInfinity(d.Remaining) || d.Remaining<0 || d.Remaining>7200 || (d.Running && d.Deadline==default(DateTime))) throw new Exception("Некорректный файл сада.");
  foreach(var p in d.Plants) if(p==null || p.Task==null || p.Task.Length>120 || p.Minutes<1 || p.Minutes>120 || p.Kind<0 || p.Kind>3) throw new Exception("Некорректные растения.");
 }
 static string Format(double seconds) { int s=(int)Math.Ceiling(seconds); return (s/60).ToString("00")+":"+(s%60).ToString("00"); }
 static double Seconds(DateTime deadline) { return Math.Max(0,(deadline-DateTime.UtcNow).TotalSeconds); }
 public Garden() {
  Text="Focus Garden"; ClientSize=new Size(1060,760); FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",10); DoubleBuffered=true;
  try { if(File.Exists(savePath)) using(var f=File.OpenRead(savePath)) { data=(SaveData)Serializer().ReadObject(f); Validate(data); } } catch { data=new SaveData(); MessageBox.Show("Не удалось прочитать сохранение. Исходный файл сохранён; создана резервная копия при следующем сохранении.","Focus Garden"); }
  title=LabelAt(this,"✳  focus garden",30,22,400,46,25,FontStyle.Bold);
  tagline=LabelAt(this,"МЕНЬШЕ ШУМА. БОЛЬШЕ РОСТА.",34,78,600,28,10,FontStyle.Regular);
  theme=ButtonAt(this,"Светлая тема",820,28,200,38,delegate { data.Dark=!data.Dark; ApplyTheme(); Save(); });
  soundButton=ButtonAt(this,"Звук: вкл.",820,76,200,32,delegate { sound=!sound; soundButton.Text=sound?"Звук: вкл.":"Звук: выкл."; });
  timerPanel.SetBounds(30,130,430,430); Controls.Add(timerPanel);
  gardenPanel.SetBounds(480,130,550,430); Controls.Add(gardenPanel);
  LabelAt(timerPanel,"01 / ВРЕМЯ ДЛЯ ВАЖНОГО",24,20,370,24,10,FontStyle.Regular);
  mode=ButtonAt(timerPanel,"Фокус · переключить на отдых",24,58,382,36,delegate { if(data.Running) { hint.Text="Поставь таймер на паузу перед сменой режима."; return; } data.Rest=!data.Rest; SetDuration(data.Rest?5:25); });
  clock=LabelAt(timerPanel,"25:00",20,102,390,98,64,FontStyle.Regular); clock.TextAlign=ContentAlignment.MiddleCenter;
  int x=24; foreach(int value in new[]{15,25,50,1}) { int minutes=value; var b=ButtonAt(timerPanel,value==1?"1 · демо":value+" мин",x,214,88,34,delegate { if(!data.Running) SetDuration(minutes); }); durations.Add(b); x+=98; }
  taskLabel=LabelAt(timerPanel,"Что вырастим сегодня?",24,266,380,24,10,FontStyle.Regular);
  task.SetBounds(24,295,382,30); task.MaxLength=120; task.Text=data.Task??""; timerPanel.Controls.Add(task);
  task.TextChanged+=delegate { data.Task=task.Text; if(!loading) Save(); };
  start=ButtonAt(timerPanel,"Начать фокус  ↗",24,350,280,48,Toggle);
  reset=ButtonAt(timerPanel,"Сбросить",314,350,92,48,delegate { data.Running=false; data.Remaining=data.Minutes*60; Save(); Render(); });
  gardenLabel=LabelAt(gardenPanel,"02 / ТВОЙ МАЛЕНЬКИЙ МИР",24,20,490,28,10,FontStyle.Regular);
  LabelAt(gardenPanel,"Хорошим идеям нужно время.",24,64,500,42,20,FontStyle.Bold);
  canvas.SetBounds(0,120,550,230); gardenPanel.Controls.Add(canvas); canvas.Paint+=DrawGarden;
  stats=LabelAt(gardenPanel,"",20,365,510,42,15,FontStyle.Regular); stats.TextAlign=ContentAlignment.MiddleCenter;
  hint=LabelAt(this,"Можно закрыть приложение. Сад останется здесь.",32,570,990,28,10,FontStyle.Regular); hint.AccessibleRole=AccessibleRole.StaticText;
  LabelAt(this,"Маленькие победы",32,610,400,28,16,FontStyle.Bold);
  history.SetBounds(32,648,698,82); history.BorderStyle=BorderStyle.None; history.HorizontalScrollbar=true; Controls.Add(history);
  export=ButtonAt(this,"↓ Экспорт сада",760,640,270,38,Export);
  import=ButtonAt(this,"↑ Импорт сада",760,688,270,38,Import);
  Resize+=delegate { int extra=ClientSize.Width-1060; theme.Left=820+extra; soundButton.Left=820+extra; };
  FormClosing+=delegate { if(data.Running) data.Remaining=Seconds(data.Deadline); Save(); };
  loading=false; ApplyTheme(); Render(); tick.Interval=250; tick.Tick+=delegate { UpdateTimer(); }; tick.Start(); UpdateTimer();
 }
 Label LabelAt(Control parent,string text,int x,int y,int w,int h,int size,FontStyle style) { var l=new Label { Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Segoe UI",size,style),BackColor=Color.Transparent }; parent.Controls.Add(l); return l; }
 Button ButtonAt(Control parent,string text,int x,int y,int w,int h,Action action) { var b=new Button { Text=text,Location=new Point(x,y),Size=new Size(w,h),FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,TabStop=true }; b.FlatAppearance.BorderSize=0; b.Click+=delegate { action(); }; parent.Controls.Add(b); return b; }
 void ApplyTheme() {
  bg=ColorTranslator.FromHtml(data.Dark?"#111C18":"#F6F5EE"); panel=ColorTranslator.FromHtml(data.Dark?"#1B2B23":"#FDFDF7"); ink=ColorTranslator.FromHtml(data.Dark?"#E9EFDF":"#20392D"); muted=ColorTranslator.FromHtml(data.Dark?"#A5B69A":"#6C7B63"); border=ColorTranslator.FromHtml(data.Dark?"#2A4032":"#E5EAD9"); accent=ColorTranslator.FromHtml(data.Dark?"#B7CF89":"#254B37");
  BackColor=bg; PaintControls(this); timerPanel.BackColor=panel; gardenPanel.BackColor=panel; canvas.BackColor=panel;
  theme.Text=data.Dark?"☀  Светлая тема":"☾  Тёмная тема"; start.BackColor=accent; start.ForeColor=data.Dark?bg:Color.White; hint.ForeColor=muted; tagline.ForeColor=muted; stats.ForeColor=muted; canvas.Invalidate();
 }
 void PaintControls(Control root) { foreach(Control c in root.Controls) { c.ForeColor=ink; if(c is Button) c.BackColor=border; else if(c is TextBox || c is ListBox) c.BackColor=panel; PaintControls(c); } }
 void SetDuration(int minutes) { data.Minutes=minutes; data.Remaining=minutes*60; data.Running=false; Save(); Render(); }
 void Toggle() { if(data.Running) { data.Remaining=Seconds(data.Deadline); data.Running=false; hint.Text="Пауза. Продолжай, когда будешь готов."; } else { data.Deadline=DateTime.UtcNow.AddSeconds(data.Remaining); data.Running=true; hint.Text="Занимайся своим делом. Мы присмотрим за временем."; } Save(); Render(); }
 void UpdateTimer() {
  if(data.Running) {
   data.Remaining=Seconds(data.Deadline);
   if(data.Remaining<=0) { data.Running=false; if(!data.Rest) { data.Plants.Add(new Plant { Task=task.Text.Trim(),Minutes=data.Minutes,Date=DateTime.UtcNow,Kind=new Random().Next(4) }); hint.Text="Новый росток! Теперь можно немного отдохнуть."; } else hint.Text="Передышка закончилась. Готов к следующему ростку?"; data.Remaining=data.Minutes*60; Save(); if(sound) System.Media.SystemSounds.Asterisk.Play(); Render(); }
  }
  clock.Text=Format(data.Remaining); Text=data.Running?clock.Text+" · Focus Garden":"Focus Garden";
 }
 void Render() {
  clock.Text=Format(data.Remaining); start.Text=data.Running?"Пауза  Ⅱ":data.Remaining<data.Minutes*60?"Продолжить  ↗":data.Rest?"Начать передышку":"Начать фокус  ↗";
  mode.Text=data.Rest?"Передышка · переключить на фокус":"Фокус · переключить на отдых";
  foreach(var b in durations) { b.Enabled=!data.Running; b.ForeColor=ink; b.BackColor=border; }
  stats.Text=data.Plants.Count+" растений     /     "+data.Plants.Sum(p=>p.Minutes)+" минут     /     "+data.Plants.Count(p=>p.Date.ToLocalTime().Date==DateTime.Today)+" сегодня";
  history.Items.Clear(); foreach(var p in data.Plants.AsEnumerable().Reverse().Take(30)) history.Items.Add(p.Date.ToLocalTime().ToString("dd.MM HH:mm")+"   ✳   "+(p.Task.Length==0?"Время для себя":p.Task)+"   +"+p.Minutes+" мин"); if(data.Plants.Count==0) history.Items.Add("Заверши первую сессию — и здесь появится первая история."); canvas.Invalidate();
 }
 void Save() { try { Directory.CreateDirectory(Path.GetDirectoryName(savePath)); string tmp=savePath+".tmp"; using(var f=File.Create(tmp)) Serializer().WriteObject(f,data); if(File.Exists(savePath)) File.Replace(tmp,savePath,savePath+".bak"); else File.Move(tmp,savePath); } catch(Exception e) { hint.Text="Не удалось сохранить сад: "+e.Message; } }
 void Export() { using(var dialog=new SaveFileDialog { Filter="Focus Garden (*.json)|*.json",FileName="focus-garden-"+DateTime.Today.ToString("yyyy-MM-dd")+".json" }) if(dialog.ShowDialog()==DialogResult.OK) try { using(var f=File.Create(dialog.FileName)) Serializer().WriteObject(f,data); hint.Text="Резервная копия сохранена."; } catch(Exception e) { MessageBox.Show(e.Message,"Ошибка экспорта"); } }
 void Import() { using(var dialog=new OpenFileDialog { Filter="Focus Garden (*.json)|*.json" }) if(dialog.ShowDialog()==DialogResult.OK) try { if(new FileInfo(dialog.FileName).Length>5000000) throw new Exception("Файл больше 5 МБ."); SaveData incoming; using(var f=File.OpenRead(dialog.FileName)) incoming=(SaveData)Serializer().ReadObject(f); Validate(incoming); var merged=data.Plants.Concat(incoming.Plants).GroupBy(p=>p.Date.ToString("o")+"|"+p.Task+"|"+p.Minutes).Select(g=>g.First()).OrderBy(p=>p.Date).ToList(); if(merged.Count>10000) throw new Exception("Максимум 10000 растений."); data.Plants=merged; Save(); Render(); hint.Text="Сад импортирован, повторяющиеся растения объединены."; } catch(Exception e) { MessageBox.Show(e.Message,"Не удалось импортировать сад"); } }
 void DrawGarden(object sender,PaintEventArgs e) {
  Graphics g=e.Graphics; g.SmoothingMode=SmoothingMode.None;
  using(var sky=new LinearGradientBrush(canvas.ClientRectangle,data.Dark?Color.FromArgb(24,43,36):Color.FromArgb(239,242,230),data.Dark?Color.FromArgb(43,66,46):Color.FromArgb(219,232,190),90f)) g.FillRectangle(sky,canvas.ClientRectangle);
  using(var sun=new SolidBrush(data.Dark?Color.FromArgb(222,231,190):Color.FromArgb(232,207,135))) g.FillEllipse(sun,447,22,42,42);
  using(var hill=new SolidBrush(data.Dark?Color.FromArgb(45,69,47):Color.FromArgb(197,211,172))) g.FillEllipse(hill,-100,110,600,230);
  using(var hill=new SolidBrush(data.Dark?Color.FromArgb(56,81,49):Color.FromArgb(179,197,149))) g.FillEllipse(hill,180,140,550,200);
  var visible=data.Plants.Skip(Math.Max(0,data.Plants.Count-18)).ToList();
  for(int i=0;i<visible.Count;i++) { int x=30+(i%9)*55,y=visible.Count<=9?170:125+(i/9)*60; DrawPlant(g,x,y,visible[i].Kind); }
  using(var brush=new SolidBrush(data.Dark?Color.FromArgb(195,210,170):Color.FromArgb(90,117,66))) using(var font=new Font("Segoe UI",9)) { string caption=visible.Count==0?"Твой первый росток уже близко":"Каждый росток — маленькая победа"; var size=g.MeasureString(caption,font); g.DrawString(caption,font,brush,(550-size.Width)/2,210); }
 }
 void DrawPlant(Graphics g,int x,int y,int kind) {
  Fill(g,"#416F43",x+18,y-28,4,32); Fill(g,"#78A059",x+8,y-16,10,5); Fill(g,"#78A059",x+22,y-24,10,5);
  if(kind==1) { Fill(g,"#7C9E50",x+14,y-50,12,10); Fill(g,"#7C9E50",x+9,y-40,22,9); Fill(g,"#7C9E50",x+4,y-31,32,10); }
  else if(kind==3) { Fill(g,"#B57851",x+8,y-43,24,6); Fill(g,"#B57851",x+3,y-37,34,13); Fill(g,"#F3DFB3",x+10,y-35,5,5); Fill(g,"#F3DFB3",x+24,y-30,4,4); }
  else { string color=kind==0?"#E8BD64":"#C28CA1"; Fill(g,color,x+12,y-47,16,25); Fill(g,color,x+6,y-41,28,13); Fill(g,"#EFDC91",x+17,y-37,6,6); }
  Fill(g,"#839861",x+6,y+4,28,4);
 }
 void Fill(Graphics g,string color,int x,int y,int w,int h) { using(var b=new SolidBrush(ColorTranslator.FromHtml(color))) g.FillRectangle(b,x,y,w,h); }
}
public class Canvas : Panel { public Canvas() { DoubleBuffered=true; } }
