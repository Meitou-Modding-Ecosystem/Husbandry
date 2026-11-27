import numpy as np, sys
from PIL import Image, ImageFilter
from lab import *
EYES=[(66,213),(172,290)]  # eye centres in this model's base-colour UV (1024px)
def sig(x): return 1/(1+np.exp(-x))
def masks(path):
    im=Image.open(path).convert('RGB')
    rgb=np.asarray(im).astype(float)/255
    lab=rgb2lab(rgb); L=lab[...,0]
    used=L>8
    bl=rgb2lab(np.asarray(im.filter(ImageFilter.GaussianBlur(4))).astype(float)/255)
    bL,bA,bB=bl[...,0],bl[...,1],bl[...,2]
    pink=sig((bA-8.5)/1.0)
    belly=sig((bL-58)/2.5+(13-bB)/1.5)*(1-pink)
    body=(1-pink)*(1-belly)
    w={'body':body*used,'belly':belly*used,'pink':pink*used}
    Lb=np.asarray(Image.fromarray(np.clip(L*2.55,0,255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(10))).astype(float)/2.55
    spot=np.clip((Lb-L-5)/7,0,1)*w['body']
    yy,xx=np.mgrid[:L.shape[0],:L.shape[1]]
    eye=np.zeros_like(L)
    for cx,cy in EYES: eye=np.maximum(eye,np.clip((15-np.hypot(xx-cx,yy-cy))/3,0,1))
    spot*=1-eye
    w['eye']=eye
    return rgb,lab,w,spot,used
if __name__=='__main__':
    rgb,lab,w,spot,used=masks(sys.argv[1])
    out=np.zeros(rgb.shape); out[...,1]=w['body']; out[...,0]=w['pink']; out[...,2]=w['belly']
    out=out*0.85+spot[...,None]*np.array([1,1,0])*0.6
    Image.fromarray((np.clip(out,0,1)*255).astype(np.uint8)).save(sys.argv[2])
    for k in w: print(k, (w[k][used]).mean())
    print('spot', spot[used].mean())
