import json,struct,sys
def swap(src,dst,imgpng):
    b=open(src,'rb').read(); l=struct.unpack('<I',b[12:16])[0]; j=json.loads(b[20:20+l]); binb=b[20+l+8:]
    mat=j['materials'][0]; ti=mat['pbrMetallicRoughness']['baseColorTexture']['index']; ii=j['textures'][ti]['source']
    target=j['images'][ii]['bufferView']; new=open(imgpng,'rb').read()
    out=bytearray()
    for i,bv in enumerate(j['bufferViews']):
        o=bv.get('byteOffset',0); data=new if i==target else binb[o:o+bv['byteLength']]
        while len(out)%4: out+=b'\0'
        bv['byteOffset']=len(out); bv['byteLength']=len(data); out+=data
    while len(out)%4: out+=b'\0'
    j['buffers'][0]['byteLength']=len(out)
    js=json.dumps(j,separators=(',',':')).encode(); js+=b' '*((4-len(js)%4)%4)
    total=12+8+len(js)+8+len(out)
    open(dst,'wb').write(struct.pack('<III',0x46546C67,2,total)+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(out),b'BIN\0')+bytes(out))
if __name__=='__main__': swap(*sys.argv[1:4])
