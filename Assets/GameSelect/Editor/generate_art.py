"""Deterministic vector-style UI artwork. Run with Python + Pillow to regenerate."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import math

OUT = Path(__file__).resolve().parent.parent / 'Art'
OUT.mkdir(exist_ok=True)
for name in ['focus-round', 'focus-circle']:
    focus = Image.new('RGBA', (1024, 1024))
    fd = ImageDraw.Draw(focus)
    if name == 'focus-round':
        fd.rounded_rectangle((4, 4, 1020, 1020), radius=152, outline='white', width=12)
    else:
        fd.ellipse((4, 4, 1020, 1020), outline='white', width=16)
    focus.resize((256, 256), Image.Resampling.LANCZOS).save(OUT / (name + '.png'))
S = 3
INK = (29, 29, 28, 255)

def save(im, name):
    im.resize((im.width//S, im.height//S), Image.Resampling.LANCZOS).save(OUT / (name+'.png'))

def canvas(n=384): return Image.new('RGBA', (n*S,n*S))
def box(b): return tuple(int(v*S) for v in b)
def ellipse(d,b,fill=INK): d.ellipse(box(b),fill=fill)
def rect(d,b,fill=INK): d.rectangle(box(b),fill=fill)
def poly(d,p,fill=INK): d.polygon([(int(x*S),int(y*S)) for x,y in p],fill=fill)
def line(d,p,width=8,fill=INK): d.line([(int(x*S),int(y*S)) for x,y in p],fill=fill,width=width*S,joint='curve')
def rounded(d,b,r,fill=INK,outline=None,width=1): d.rounded_rectangle(box(b),r*S,fill=fill,outline=outline,width=width*S)

im=canvas(256); d=ImageDraw.Draw(im); rounded(d,(0,0,256,256),30,(255,255,255,255)); save(im,'round')
im=canvas(256); d=ImageDraw.Draw(im); rounded(d,(2,2,254,254),30,INK); rounded(d,(7,7,249,249),25,(255,255,255,255)); save(im,'selected')
im=canvas(256); d=ImageDraw.Draw(im); ellipse(d,(0,0,256,256),(255,255,255,255)); save(im,'circle')
im=canvas(256); d=ImageDraw.Draw(im); rounded(d,(28,22,228,224),30,(0,0,0,48)); im=im.filter(ImageFilter.GaussianBlur(14*S)); save(im,'shadow')

im=canvas(); d=ImageDraw.Draw(im)
ellipse(d,(65,62,325,322))
for b in [(229,20,311,100),(266,61,360,154),(298,121,391,212)]: ellipse(d,b,(0,0,0,0))
for x,y,r in [(161,125,16),(121,192,17),(205,172,16),(170,251,17),(252,225,17)]: ellipse(d,(x-r,y-r,x+r,y+r),(0,0,0,0))
save(im,'cookie')

im=canvas()
for angle,offset in [(21,(-56,20)),(-21,(58,20))]:
    card=canvas(); d=ImageDraw.Draw(card); rounded(d,(111,69,273,307),14)
    card=card.rotate(angle,Image.Resampling.BICUBIC)
    im.alpha_composite(card,(offset[0]*S,offset[1]*S))
d=ImageDraw.Draw(im); rounded(d,(99,54,285,316),20,(255,255,255,255)); rounded(d,(106,61,278,309),14,(255,255,255,255),INK,7)
# Spade: two lobes, pointed crown and a tapering stem.
poly(d,[(192,115),(135,175),(249,175)])
ellipse(d,(132,153,198,223)); ellipse(d,(186,153,252,223))
poly(d,[(190,194),(194,194),(203,243),(219,256),(165,256),(181,243)])
for x,y,r in [(327,53,25),(44,280,20)]:
    poly(d,[(x,y-r),(x+7,y-7),(x+r,y),(x+7,y+7),(x,y+r),(x-7,y+7),(x-r,y),(x-7,y-7)])
save(im,'cards')

im=canvas(); d=ImageDraw.Draw(im)
poly(d,[(72,80),(96,86),(253,86),(275,80),(271,104),(258,113),(89,113),(76,105)])
rect(d,(112,112,136,266)); rect(d,(221,112,245,207)); rect(d,(86,136,264,158))
ticket=canvas(); td=ImageDraw.Draw(ticket); rounded(td,(216,196,300,320),7,(255,255,255,255),INK,8)
rect(td,(239,218,278,229)); rect(td,(254,225,264,289)); ticket=ticket.rotate(-20,Image.Resampling.BICUBIC,center=(258*S,258*S)); im.alpha_composite(ticket)
d=ImageDraw.Draw(im)
for x,y in [(60,260),(314,166)]:
    for a in range(5):
        xx=x+15*math.sin(a*math.tau/5); yy=y+15*math.cos(a*math.tau/5)
        ellipse(d,(xx-8,yy-10,xx+8,yy+10))
    ellipse(d,(x-4,y-4,x+4,y+4),(255,255,255,255))
save(im,'shrine')

im=canvas(); d=ImageDraw.Draw(im); rounded(d,(60,115,324,275),58)
rect(d,(112,155,130,227),(255,255,255,255)); rect(d,(86,182,156,200),(255,255,255,255))
ellipse(d,(245,162,264,181),(255,255,255,255)); ellipse(d,(272,201,291,220),(255,255,255,255)); save(im,'gamepad')
im=canvas(96); d=ImageDraw.Draw(im); line(d,[(56,23),(32,48),(56,73)],8); save(im,'chevron')
im=canvas(96); d=ImageDraw.Draw(im); poly(d,[(26,15),(78,48),(26,81)],(255,255,255,255)); save(im,'play')
im=canvas(96); d=ImageDraw.Draw(im)
ellipse(d,(12,12,42,42)); ellipse(d,(52,12,82,42)); rounded(d,(4,46,49,79),14); rounded(d,(49,46,94,79),14); save(im,'players')

im=Image.new('RGBA',(1920*S,1080*S),(249,249,248,255)); d=ImageDraw.Draw(im)
def curve(points):
    p=[]
    for i in range(241):
        t=i/240; u=1-t
        p.append((u**3*points[0][0]+3*u*u*t*points[1][0]+3*u*t*t*points[2][0]+t**3*points[3][0],u**3*points[0][1]+3*u*u*t*points[1][1]+3*u*t*t*points[2][1]+t**3*points[3][1]))
    line(d,p,1,(219,219,216,255))
for p in [[(-100,201),(640,-64),(832,511),(2000,145)],[(-100,720),(720,342),(1390,-25),(2020,362)],[(-100,900),(650,660),(1470,693),(2030,877)],[(-100,1040),(910,738),(1300,758),(1980,1130)]]: curve(p)
save(im,'background')
