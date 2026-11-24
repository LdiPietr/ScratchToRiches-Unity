from pathlib import Path
from PIL import Image
import json, re
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
data=json.loads((ROOT/'Design/S02/vintage-layout.json').read_text())
base=ROOT/'Assets/Art/UI'
def path(key):
    if key.startswith('Classico/'):
        name=key.split('/')[1]
        name={'Ticket':'Ticket_Classic','ScratchCover':'ScratchCover_Classic'}.get(name,name)
        return base/'S02_Classic/Ticket'/f'{name}.png'
    return base/'VintageLottery/Tickets'/f'{key}.png'
def image(key):
    p=path(key)
    assert re.search(r'^guid: '+data['guids'][key]+r'$',p.with_suffix('.png.meta').read_text(),re.M)
    return Image.open(p).convert('RGBA')
for t,n in zip(data['tiers'],[9,16,25,25]):
    W,H=t['size']; w,h=t['cell']; cw,ch=t['coverSize']; size=t['symbol']
    assert len(t['centers'])==n
    for key,target in [('artwork',(W,H)),('frame',(w,h)),('revealed',(cw,ch)),('cover',(cw,ch))]:
        a=image(t[key]); assert a.width*target[1]==a.height*target[0],(t['n'],key,a.size,target)
    if t['mask']:
        m=image(t['mask'])
        assert m.width*ch==m.height*cw
        mask=np.asarray(m.getchannel('A'))
        assert mask[0,0]<128 and mask[mask.shape[0]//2,mask.shape[1]//2]>=128
        assert 'isReadable: 1' in path(t['mask']).with_suffix('.png.meta').read_text()
    else:
        m=Image.new('RGBA',(cw,ch),(255,255,255,255))
    assert 'isReadable: 1' in path(t['cover']).with_suffix('.png.meta').read_text()
    coverage=np.asarray(m.getchannel('A').resize((128,128),Image.Resampling.NEAREST))>=128
    assert coverage.sum()>0 and (coverage.sum()==128**2 if n==9 else coverage.sum()<128**2)
    # Verify that the entire symbol box is inside the opaque scratch shape.
    mx,my=m.size; sx=int(np.ceil(size/cw*mx/2)); sy=int(np.ceil(size/ch*my/2))
    assert np.asarray(m.getchannel('A'))[my//2-sy:my//2+sy,mx//2-sx:mx//2+sx].min()>=128,(t['n'],'symbol outside foil')
    board=np.zeros((H*2,W*2),dtype=np.uint8)
    frame=np.asarray(image(t['frame']).getchannel('A').resize((w*2,h*2),Image.Resampling.NEAREST))>=128
    if t['mask']:
        covered=np.asarray(m.getchannel('A').resize((w*2,h*2),Image.Resampling.NEAREST))>=128
    else:
        covered=np.zeros((h*2,w*2),dtype=bool)
        x0=w-cw; y0=h-ch; covered[y0:y0+ch*2,x0:x0+cw*2]=True
    shape=frame|covered
    for cx,cy in t['centers']:
        x,y=round((cx-w/2)*2),round((cy-h/2)*2)
        assert x>=0 and y>=0 and x+w*2<=W*2 and y+h*2<=H*2
        region=board[y:y+h*2,x:x+w*2]
        assert not np.any((region>0)&shape),(t['n'],'overlap',cx,cy)
        region[shape]=1
    print('PASS',t['n'],n,'cells, source ratios, mask coverage',round(100*coverage.mean(),2),'%, no shape overlaps')
for name in ['ScratchToRiches','ScratchTicketLab']:
    s=(ROOT/'Assets/Scenes'/f'{name}.unity').read_text()
    assert len(re.findall(r'^  m_Name: Cell_\d+$',s,re.M))==25
    assert s.count('  vintageRevealedBackground: ')==25
    assert s.count('  vintageTickets:')==1
    for t in data['tiers']:
        for key in ['artwork','frame','revealed','cover','mask']:
            if t[key]: assert data['guids'][t[key]] in s
    ids=re.findall(r'^--- !u!\d+ &(\d+)',s,re.M)
    assert len(ids)==len(set(ids))
    missing=set(re.findall(r'\{fileID: (\d+)\}',s))-set(ids)-{'0'}
    assert not missing,(name,missing)
    if name=='ScratchTicketLab':
        assert '::ScratchTicketLab\n' in s and '::ScratchGameManager\n' not in s and '::ScratchEconomyManager\n' not in s
    print('PASS',name,'scene links and authored cells')
