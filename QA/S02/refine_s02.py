from pathlib import Path
import hashlib, json
import numpy as np
from PIL import Image, ImageDraw, ImageFont

def refine(root):
    art = root / 'Assets/Art/UI/S02_Classic'
    names = ['Branding/Logo_HUD', 'Ticket/Ticket_Classic', 'Ticket/ScratchCover_Classic']
    before = {n: Image.open(art / (n+'.png')).convert('RGBA') for n in names}
    metas = {n: (art/(n+'.png.meta')).read_bytes() for n in names}
    expected = [(620,96), (1056,1200), (430,320)]
    assert [im.size for im in before.values()] == expected
    rng = np.random.default_rng(260124)
    def noise(w,h,cw,ch):
        a = rng.integers(0,256,(ch,cw),dtype=np.uint8)
        return np.asarray(Image.fromarray(a).resize((w,h),Image.Resampling.BILINEAR),dtype=np.float32)/255-.5
    a = np.array(before[names[1]], dtype=np.int16); w,h=before[names[1]].size
    grain = noise(w,h,150,170)*8 + noise(w,h,26,30)*5 + rng.integers(-2,3,(h,w))
    y,x=np.indices((h,w)); ink=(a[:,:,0]-a[:,:,1]>45) & (a[:,:,1]<175)
    grain -= (((x%9-4)**2+(y%9-4)**2)<=1)*ink*3
    a[:,:,:3]=np.clip(a[:,:,:3]+np.rint(grain)[:,:,None],0,255)
    paper=Image.fromarray(a.astype(np.uint8),'RGBA')
    a=np.array(before[names[2]],dtype=np.float32); w,h=before[names[2]].size
    mark=np.clip((179-a[:,:,0])/62,0,1)
    gray=184+noise(w,h,10,9)*17+noise(w,h,120,14)*13+noise(w,h,35,80)*9+rng.integers(-5,6,(h,w))
    gray=np.rint(gray/2)*2
    base=np.stack([gray,gray+3,gray+1],axis=-1)
    a[:,:,:3]=np.clip(np.rint(base*(1-mark[:,:,None])+a[:,:,:3]*mark[:,:,None]),0,255)
    foil=Image.fromarray(a.astype(np.uint8),'RGBA')
    font_paths=[Path('/usr/share/fonts/truetype/liberation2/LiberationSans-BoldItalic.ttf'),Path('/usr/share/fonts/truetype/liberation/LiberationSans-BoldItalic.ttf')]
    fp=next(p for p in font_paths if p.exists()); name='Scratchaholic!'
    logo=Image.new('RGBA',expected[0]); d=ImageDraw.Draw(logo)
    for size in range(79,30,-1):
        f=ImageFont.truetype(str(fp),size); b=d.textbbox((0,0),name,font=f,stroke_width=3)
        if b[2]-b[0]<=580 and b[3]-b[1]<=76: break
    x=7-b[0]; y=(96-(b[3]-b[1]))//2-b[1]-2
    d.text((x+3,y+5),name,font=f,fill='#12281F',stroke_width=4,stroke_fill='#12281F')
    d.text((x,y),name,font=f,fill='#F8E5B0',stroke_width=3,stroke_fill='#994032')
    report=[]
    for n,im,dim in zip(names,[logo,paper,foil],expected):
        assert im.size==dim and im.mode=='RGBA'
        assert im.getchannel('A').getextrema()==((0,255) if n==names[0] else (255,255))
        p=art/(n+'.png'); im.save(p,optimize=True)
        with Image.open(p) as check: check.verify()
        assert (art/(n+'.png.meta')).read_bytes()==metas[n]
        report.append({'asset':n,'size':dim,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'alpha':im.getchannel('A').getextrema(),'meta':'unchanged'})
    return report

if __name__=='__main__':
    print(json.dumps({'assets':refine(Path('.'))},indent=2))
