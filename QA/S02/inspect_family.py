from pathlib import Path
from PIL import Image
import numpy as np
for p in sorted(Path('Assets/Art/UI/VintageLottery/Tickets').iterdir()):
 if not p.is_dir(): continue
 print(p.name, flush=True)
 for x in sorted(p.glob('*.png')):
  im=Image.open(x).convert('RGBA'); a=np.asarray(im)
  print(x.name,im.size,'alpha',im.getchannel('A').getextrema(),'bbox',im.getbbox(),'center',im.getpixel((im.width//2,im.height//2)),flush=True)
  if x.name=='ScratchMask.png': print('mask corners',a[0,0].tolist(),a[-1,-1].tolist(),'coverage',float(np.mean(a[:,:,3]>127)),flush=True)
  if x.name=='Ticket.png':
   im.thumbnail((220,220)); im.save('geometry/'+p.name+'.png')
   r,g,b=[a[:,:,i].astype(int) for i in range(3)]
   light=(r>155)&(g>145)&(b>100)&(abs(r-g)<80)&(g-b<80)
   print('row light proportions:',[(int(y/a.shape[0]*100),round(float(light[y].mean()),3)) for y in range(0,a.shape[0],a.shape[0]//50)],flush=True)
   for y in [int(a.shape[0]*v) for v in (.30,.33,.35,.40,.45,.5,.6,.7,.8,.85,.88,.90,.92)]:
    print('row',y,[(int(x),a[y,x,:3].tolist()) for x in [int(a.shape[1]*v) for v in (.02,.04,.06,.08,.10,.15,.25,.5,.75,.9,.94,.96,.98)]],flush=True)
