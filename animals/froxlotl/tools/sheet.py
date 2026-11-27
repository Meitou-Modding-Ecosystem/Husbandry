import sys, os, json, numpy as np
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(__file__))
from recolour import SCHEMES
from lab import lab2rgb
R=sys.argv[1]; OUT=sys.argv[2]
F=lambda s,b=False: ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans%s.ttf'%('-Bold' if b else ''),s)
BG=(38,39,46); FG=(235,235,240); SUB=(160,162,175)
CW,HH,SH=620,440,170
def crop(p):
    im=Image.open(p); bb=im.getchannel('A').point(lambda a:255 if a>8 else 0).getbbox(); return im.crop(bb)
def fit(im,w,h):
    s=min(w/im.width,h/im.height); return im.resize((int(im.width*s),int(im.height*s)),Image.LANCZOS)
def sw(lab): return tuple(int(v*255) for v in lab2rgb(np.array(lab,float)))
def cell(slug,name,cols):
    c=Image.new('RGBA',(CW,HH+SH+70),BG+(255,))
    h=fit(crop(f'{R}/{slug}_20_35.png'),CW-40,HH-10); c.alpha_composite(h,((CW-h.width)//2,(HH-h.height)//2+5))
    s=fit(crop(f'{R}/{slug}_70_m8.png'),CW-40,SH-20); c.alpha_composite(s,((CW-s.width)//2,HH+(SH-s.height)//2))
    d=ImageDraw.Draw(c); y=HH+SH+12
    d.text((24,y),name,font=F(26,True),fill=FG)
    x=CW-24
    for col in reversed(cols):
        x-=34; d.rounded_rectangle((x,y+2,x+28,y+30),6,fill=col,outline=(90,92,100))
    return c
groups={'Pastel':[],'Tropical':[]}
for n,sc in SCHEMES.items(): groups[sc[0].title()].append((n,sc))
ncol=3; rows=sum((len(v)+ncol-1)//ncol for v in groups.values())
cellh=HH+SH+70
W=ncol*CW+40; H=90+cellh+60+rows*cellh+len(groups)*70+30
sheet=Image.new('RGBA',(W,H),BG+(255,)); d=ImageDraw.Draw(sheet)
d.text((30,24),'Froxlotl colour variants',font=F(40,True),fill=FG)
d.text((30+620+20,40),'body · belly/feet · gills  (swatches)   —   spots recoloured on some schemes',font=F(20),fill=SUB)
y=90
sheet.alpha_composite(cell('original','Original',[(110,106,72),(180,165,150),(165,110,110)]),(20+CW,y)); y+=cellh+30
for g,items in groups.items():
    d.text((30,y+20),g,font=F(32,True),fill=FG); y+=70
    for i,(n,sc) in enumerate(items):
        cols=[sw(sc[1]),sw(sc[2]),sw(sc[3])]+([sw(sc[4])] if sc[4] else [])
        sheet.alpha_composite(cell(n.lower().replace(' ','-'),n,cols),(20+(i%ncol)*CW,y+(i//ncol)*cellh))
    y+=((len(items)+ncol-1)//ncol)*cellh
sheet.convert('RGB').save(OUT)
print(sheet.size)
