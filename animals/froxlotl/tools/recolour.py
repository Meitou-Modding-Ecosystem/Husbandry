import numpy as np, sys, os, json
from PIL import Image
from lab import *
from masks import masks
# name: (group, body Lab, belly Lab, gills/feet Lab, spot Lab or None, contrast)
SCHEMES={
 'Strawberry Milk':('pastel',(80,22,4),(93,4,6),(70,42,12),None,0.7),
 'Lavender':('pastel',(76,14,-20),(93,3,-2),(78,30,-4),(64,22,-30),0.7),
 'Mint':('pastel',(84,-24,8),(95,-2,6),(80,22,22),None,0.7),
 'Baby Blue':('pastel',(80,-6,-20),(95,0,-2),(90,-4,40),(68,-4,-30),0.7),
 'Buttercream':('pastel',(90,-2,32),(96,0,6),(80,28,18),None,0.7),
 'Peach Sorbet':('pastel',(82,18,24),(94,4,10),(74,34,-6),None,0.7),
 'Parrot':('tropical',(58,-52,48),(86,-4,72),(50,66,50),(40,10,-52),1.0),
 'Sunset':('tropical',(64,48,58),(84,8,70),(48,72,-8),(42,58,40),1.0),
 'Lagoon':('tropical',(66,-40,-12),(94,-4,2),(60,70,-8),(38,-6,-42),1.0),
 'Poison Dart':('tropical',(46,22,-64),(26,2,-8),(84,-2,82),(16,0,0),1.0),
 'Mango':('tropical',(76,20,72),(92,-2,30),(50,64,50),(52,-30,40),1.0),
 'Hibiscus':('tropical',(52,66,-12),(88,18,0),(86,-4,80),(36,50,-30),1.0),
}
def recolour(rgb,lab,w,spot,used,scheme):
    _,body,belly,pink,sp,c=scheme
    L,A,B=lab[...,0],lab[...,1],lab[...,2]
    out=np.zeros_like(lab)
    for k,t in (('body',body),('belly',belly),('pink',pink)):
        wk=w[k]; s=wk.sum()+1e-6
        m=[(lab[...,i]*wk).sum()/s for i in range(3)]
        reg=np.stack([t[0]+(L-m[0])*c, t[1]+(A-m[1])*0.4, t[2]+(B-m[2])*0.4],-1)
        out+=reg*wk[...,None]
    if sp is not None:
        sv=np.clip(spot*1.6,0,1)[...,None]
        out=out*(1-sv)+np.stack([np.full_like(L,sp[0])+(L-L.mean())*0.3,np.full_like(L,sp[1]),np.full_like(L,sp[2])],-1)*sv
    res=lab2rgb(out)
    e=w['eye'][...,None]; res=res*(1-e)+rgb*e
    res=np.where(used[...,None],res,rgb)
    return (np.clip(res,0,1)*255+0.5).astype(np.uint8)
if __name__=='__main__':
    src,outdir=sys.argv[1],sys.argv[2]
    os.makedirs(outdir,exist_ok=True)
    data=masks(src)
    for name,sc in SCHEMES.items():
        Image.fromarray(recolour(*data,sc)).save(os.path.join(outdir,name.lower().replace(' ','-')+'.png'))
    json.dump({k:v[0] for k,v in SCHEMES.items()},open(os.path.join(outdir,'groups.json'),'w'))
