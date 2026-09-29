extends Node3D
## Decorative surroundings only. No collision, navigation or gameplay nodes.

var rng := RandomNumberGenerator.new()
var batches:Dictionary = {}
const PALE_FOG := Color("#293347")

func _ready():
	name="Foret_des_Brumes"
	rng.seed=2792026
	for kind in ["Terrain","Montagnes","Ecorce","Feuillage","Cristaux","Lueurs","Lisiere","Herbes","Eau_des_rives"]:
		batches[kind]={"v":PackedVector3Array(),"c":PackedColorArray(),"n":PackedVector3Array()}
	landscape()
	mountains()
	forest()
	planted_transition()
	finish_meshes()
	fireflies()
	forest_mist()
	set_meta("decorative_only",true)
	set_meta("style","Forêt des Brumes : schiste, arbres noueux, cristaux et lucioles")

func triangle(kind:String,a:Vector3,b:Vector3,c:Vector3,color:Color,shade:bool=true):
	var normal=(b-a).cross(c-a).normalized()
	# Godot uses clockwise front faces; lit vegetation needs matching normals.
	if kind=="Herbes":normal=-normal
	var brightness=.73+.27*abs(normal.dot(Vector3(-.35,.85,.4).normalized())) if shade else 1.0
	var tinted=color*Color(brightness,brightness,brightness,1)
	if kind in ["Terrain","Montagnes","Ecorce","Feuillage"]:
		tinted=Color.from_hsv(tinted.h,minf(tinted.s*1.08,.85),minf(tinted.v*1.23+.015,.72),tinted.a)
	# Distance haze baked into the distant landscape, without fog over the arena.
	var distance_fog=clampf((-((a+b+c)/3.0).z-30.0)/65.0,0,.65)
	if kind!="Lueurs" and kind!="Cristaux":tinted=tinted.lerp(PALE_FOG.lightened(.035),distance_fog)
	for p in [a,b,c]:
		batches[kind].v.append(p)
		batches[kind].c.append(tinted)
		batches[kind].n.append(normal)

func quad(kind:String,a:Vector3,b:Vector3,c:Vector3,d:Vector3,col:Color):
	triangle(kind,a,b,c,col)
	triangle(kind,a,c,d,col)

func landscape():
	# A continuous dark forest floor below and outside the existing board.
	quad("Terrain",Vector3(-85,-1.1,45),Vector3(85,-1.1,45),Vector3(85,-1.1,-105),Vector3(-85,-1.1,-105),Color("#17242c"))
	for side in [-1,1]:
		for z in range(-30,25,3):
			var x=side*rng.randf_range(13.2,17.0)
			rock(Vector3(x,-1.1,z),Vector3(rng.randf_range(2.5,4),rng.randf_range(.45,1.1),3.4),Color("#263638"),"Terrain")
	# Exposed fractured stone under the floating-looking edge of the battlefield.
	for side in [-1,1]:
		for z in range(-24,25,2):
			rock(Vector3(side*12.2,-1.7,z),Vector3(.9,1.05,1.35),Color("#33404a"),"Montagnes")
	for x in range(-12,13,3):
		rock(Vector3(x,-1.65,-24.3),Vector3(1.75,1.05,1.15),Color("#33404a"),"Montagnes")

func rock(pos:Vector3,size:Vector3,col:Color,kind:String="Montagnes"):
	var sides=7
	var rings:Array=[]
	for j in range(3):
		var ring:Array[Vector3]=[]
		for i in range(sides):
			var angle=TAU*i/sides+.13*j
			var radius=[1.0,.76,.30][j]*rng.randf_range(.86,1.10)
			ring.append(pos+Vector3(cos(angle)*size.x*radius,size.y*[0.0,.52,.86][j],sin(angle)*size.z*radius))
		rings.append(ring)
	for j in range(2):
		for i in range(sides):
			var n=(i+1)%sides
			quad(kind,rings[j][i],rings[j][n],rings[j+1][n],rings[j+1][i],col.lightened(rng.randf_range(0,.035)))
	var top=pos+Vector3(size.x*.12,size.y,size.z*.06)
	for i in range(sides):triangle(kind,rings[2][i],rings[2][(i+1)%sides],top,col.lightened(.035))

func peak(pos:Vector3,width:float,height:float,depth:float,col:Color):
	var sides=9
	var levels:Array=[]
	var lean=Vector3(rng.randf_range(-width*.28,width*.28),0,rng.randf_range(-1.2,1.2))
	for j in range(4):
		var points:Array[Vector3]=[]
		var h=[0.0,.28,.61,.83][j]
		for i in range(sides):
			var angle=TAU*i/sides+.10*j
			var r=[1.0,.72,.43,.20][j]*rng.randf_range(.8,1.18)
			points.append(pos+lean*h+Vector3(cos(angle)*width*r,height*h,sin(angle)*depth*r))
		levels.append(points)
	for j in range(3):
		for i in range(sides):
			var n=(i+1)%sides
			var facet=col.lightened(rng.randf_range(.005,.065)+.014*j)
			quad("Montagnes",levels[j][i],levels[j][n],levels[j+1][n],levels[j+1][i],facet)
	var tip=pos+lean+Vector3(0,height,0)
	for i in range(sides):triangle("Montagnes",levels[3][i],levels[3][(i+1)%sides],tip,col.lightened(.06))
	if pos.z>-48 and height>6 and rng.randf()<.45:
		var vein:Array[Vector3]=[levels[1][2]+Vector3(0,0,.025),levels[2][2]+Vector3(0,0,.025),levels[3][2]+Vector3(0,0,.025)]
		tube(vein,[.035,.055,.008],Color("#54899a"),"Lueurs")

func mountains():
	# Broken, asymmetric silhouettes rather than a regular ring of cones.
	for i in range(12):
		peak(Vector3(-48+i*8.5,-1.1,rng.randf_range(-47,-42)),rng.randf_range(7,10),rng.randf_range(2.5,5.0),rng.randf_range(4,6),Color("#232c43"))
	for i in range(9):
		peak(Vector3(-34+i*8.5,-1.1,rng.randf_range(-35,-32)),rng.randf_range(4,6.5),rng.randf_range(6,10),rng.randf_range(3,4.5),Color("#2c2e46"))
	for side in [-1,1]:
		for i in range(7):
			var z=-30+i*7.2
			var h=rng.randf_range(5,10) if z<2 else rng.randf_range(2.3,5)
			peak(Vector3(side*rng.randf_range(21,26),-1.1,z),rng.randf_range(3.3,5),h,rng.randf_range(3.5,5),Color("#242b3c"))
	# Crystal veins in selected foothills.
	for side in [-1,1]:
		for z in [-27,-12,5,17]:
			crystal_cluster(Vector3(side*rng.randf_range(14,16),-.4,z),rng.randf_range(.75,1.15))
	for x in [-19,-8,10,22]:crystal_cluster(Vector3(x,-.4,-28),1.1)

func tube(points:Array[Vector3],radii:Array[float],col:Color,kind:String="Ecorce"):
	var rings:Array=[]
	for i in range(points.size()):
		var direction=(points[mini(i+1,points.size()-1)]-points[maxi(0,i-1)]).normalized()
		var u=direction.cross(Vector3.FORWARD).normalized()
		if u.length()<.1:u=Vector3.RIGHT
		var v=direction.cross(u).normalized()
		var ring:Array[Vector3]=[]
		for j in range(6):ring.append(points[i]+radii[i]*(u*cos(TAU*j/6.0)+v*sin(TAU*j/6.0)))
		rings.append(ring)
	for i in range(points.size()-1):
		for j in range(6):quad(kind,rings[i][j],rings[i][(j+1)%6],rings[i+1][(j+1)%6],rings[i+1][j],col.lightened(.012*(j%3)))

func tree(pos:Vector3,h:float,bare:bool):
	# Keep every branch outside the board, including the closest forest row.
	if pos.z>-25:h=minf(h,maxf(1.0,(absf(pos.x)-12.8)/.62))
	var angle=rng.randf_range(0,TAU)
	var bend=Vector3(cos(angle),0,sin(angle))*h*.14
	var trunk:Array[Vector3]=[pos,pos+Vector3(0,h*.3,0)-bend*.3,pos+Vector3(0,h*.65,0)+bend*.5,pos+Vector3(0,h,0)+bend]
	var bark=Color("#32313f") if bare else Color("#252e39")
	tube(trunk,[h*.065,h*.048,h*.030,.012],bark)
	for i in range(4):
		var a=angle+TAU*i/4
		var dir=Vector3(cos(a),0,sin(a))
		tube([pos+Vector3(0,.20,0),pos+dir*h*.13,pos+dir*h*.23-Vector3(0,.08,0)],[h*.045,h*.032,.005],bark)
	for i in range(4):
		var a=angle+i*2.399
		var dir=Vector3(cos(a),0,sin(a))
		var start=pos+Vector3(0,h*(.42+i*.1),0)+bend*.3
		var elbow=start+dir*h*.22+Vector3(0,h*.02,0)
		var tip=elbow+dir*h*.09+Vector3(0,h*(.24 if bare else .13),0)
		tube([start,elbow,tip],[h*.035,h*.020,.009],bark)
		if bare:
			var fork=elbow+Vector3(-dir.z,.16,dir.x)*h*.14
			tube([elbow,fork,fork+Vector3(0,h*.12,0)],[h*.018,h*.012,.003],bark)
			if i%2==0:gem(tip-Vector3(0,.12,0),Vector3(.065,.14,.065),Color("#6ebdbb"),"Lueurs")
		else:
			var leaf=Color("#204044") if i%2==0 else Color("#34344c")
			rock(tip-Vector3(0,h*.11,0),Vector3(h*.22,h*.19,h*.23),leaf,"Feuillage")
	if not bare:
		rock(trunk[3]-Vector3(0,h*.18,0),Vector3(h*.20,h*.27,h*.19),Color("#29474b"),"Feuillage")
	# A thin turquoise scar on a few ancient trunks.
	if bare and rng.randf()<.45:
		tube([trunk[0]+Vector3(.035,h*.17,-h*.065),trunk[1]+Vector3(.03,0,-h*.048),trunk[2]+Vector3(0,-h*.08,-h*.03)],[.016,.025,.008],Color("#4b999f"),"Lueurs")

func forest():
	for side in [-1,1]:
		for row in range(3):
			for i in range(18):
				var z=-30+i*3.1+rng.randf_range(-.9,.9)
				var x=side*(14.7+row*3.2+rng.randf_range(-.5,.6))
				var h=rng.randf_range(3.0,5.6)
				if z>9:h*=.58
				tree(Vector3(x,-.45,z),h,rng.randf()<.40)
	for row in range(2):
		for i in range(18):
			tree(Vector3(-26+i*3.1+rng.randf_range(-.6,.6),-.45,-27.7-row*4+rng.randf_range(-.5,.5)),rng.randf_range(3.2,6),rng.randf()<.4)
	# Small luminous forest mushrooms, outside the board's collision limits.
	for side in [-1,1]:
		for i in range(19):
			var p=Vector3(side*rng.randf_range(13,17),-.45,rng.randf_range(-27,23))
			for j in range(3):
				var q=p+Vector3(rng.randf_range(-.4,.4),0,rng.randf_range(-.4,.4))
				var h=rng.randf_range(.15,.4)
				tube([q,q+Vector3(.025,h,0)],[.04,.025],Color("#485064"))
				rock(q+Vector3(0,h-.06,0),Vector3(.15,.10,.15),Color("#647ca8"),"Lueurs")

func gem(pos:Vector3,size:Vector3,col:Color,kind:String="Cristaux"):
	var a=pos+Vector3(-size.x,0,0)
	var b=pos+Vector3(0,0,-size.z)
	var c=pos+Vector3(size.x,0,0)
	var d=pos+Vector3(0,0,size.z)
	for edge in [[a,b],[b,c],[c,d],[d,a]]:
		triangle(kind,edge[0],edge[1],pos+Vector3(size.x*.13,size.y,0),col)
		triangle(kind,edge[1],edge[0],pos-Vector3(0,size.y*.4,0),col.darkened(.15))

func crystal_cluster(pos:Vector3,s:float):
	rock(pos-Vector3(0,.15,0),Vector3(s*.8,s*.25,s*.65),Color("#313244"))
	for j in range(4):
		var p=pos+Vector3(rng.randf_range(-.5,.5),.2,rng.randf_range(-.4,.4))*s
		gem(p,Vector3(.16,rng.randf_range(.5,1.25),.19)*s,Color("#407f91") if j%2==0 else Color("#776392"))
		gem(p+Vector3(0,.35*s,0),Vector3(.04,.22,.045)*s,Color("#8bc9d0"),"Lueurs")

func finish_meshes():
	for kind in batches:
		var b=batches[kind]
		var arrays=[]
		arrays.resize(Mesh.ARRAY_MAX)
		arrays[Mesh.ARRAY_VERTEX]=b.v
		arrays[Mesh.ARRAY_NORMAL]=b.n
		arrays[Mesh.ARRAY_COLOR]=b.c
		var mesh=ArrayMesh.new()
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
		var mat=StandardMaterial3D.new()
		mat.vertex_color_use_as_albedo=true
		mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED
		if kind in ["Lisiere","Herbes","Eau_des_rives"]:
			mat.shading_mode=BaseMaterial3D.SHADING_MODE_PER_PIXEL
			mat.roughness=.95
		mat.cull_mode=BaseMaterial3D.CULL_DISABLED
		mesh.surface_set_material(0,mat)
		var instance=MeshInstance3D.new()
		instance.name=kind
		instance.mesh=mesh
		instance.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(instance)
	# Release generation arrays after uploading the merged scenery meshes.
	batches.clear()

func ground_quad(a:Vector3,b:Vector3,c:Vector3,d:Vector3,inner:Color,outer:Color,kind:String="Lisiere"):
	# Vertex gradients join the grass to the undergrowth without straight color bands.
	var points=[a,b,c,a,c,d]
	var colors=[inner,inner,outer,inner,outer,outer]
	if (b-a).cross(c-a).y>0:
		points=[a,c,b,a,d,c]
		colors=[inner,outer,inner,inner,outer,outer]
	for i in range(6):
		batches[kind].v.append(points[i])
		batches[kind].c.append(colors[i])
		batches[kind].n.append(Vector3.UP)

func side_edge(side:float,z:float,band:int)->Vector3:
	var wobble=sin(z*.81+side*.7)*.17+sin(z*1.73)*.09
	var x=[10.0,10.95,12.20,14.9][band]+wobble*[1.0,.7,1.7,2.8][band]
	var y=[-.025,-.014,-.17,-.78][band]
	if band>0:y+=sin(z*1.14+side)*.035*band
	return Vector3(side*x,y,z)

func planted_transition():
	# Independent seed keeps changes here from moving the original forest or affecting gameplay.
	rng.seed=11042027
	var palette=[Color("#426541"),Color("#3d5d3f"),Color("#354e3f"),Color("#293c37")]
	for side in [-1,1]:
		for segment in range(96):
			var z=-24.0+segment*.5
			if z>=-2.5 and z<2.5:continue
			for band in range(3):
				ground_quad(side_edge(side,z,band),side_edge(side,z+.5,band),side_edge(side,z+.5,band+1),side_edge(side,z,band+1),palette[band],palette[band+1])
		# River continues through the bank instead of ending at the rectangular plate.
		for band in range(3):
			var x0=[10.8,11.8,13.4][band]
			var x1=[11.8,13.4,15.0][band]
			var half0=[2.0,1.9,1.65][band]
			var half1=[1.9,1.65,1.2][band]
			ground_quad(Vector3(side*x0,.083,-half0),Vector3(side*x0,.083,half0),Vector3(side*x1,.083,half1),Vector3(side*x1,.083,-half1),Color("#2b6e8e").darkened(band*.12),Color("#2b6e8e").darkened((band+1)*.12),"Eau_des_rives")
		for i in range(64):
			var z=-23.5+i*.74+rng.randf_range(-.2,.2)
			if absf(z)<2.6:continue
			var p=Vector3(side*rng.randf_range(10.65,12.75),-.01,z)
			grass_tuft(p,rng.randf_range(.14,.32))
			if i%3==0:
				var shrub=Vector3(side*rng.randf_range(11.5,12.65),-.07,z+.1)
				rock(shrub,Vector3(.46,.43,.6),Color("#3b6050"),"Herbes")
				rock(shrub+Vector3(side*.28,-.015,.26),Vector3(.36,.27,.37),Color("#4b6950"),"Herbes")
			if i%4==0:
				rock(Vector3(side*rng.randf_range(11.05,12.0),-.07,z),Vector3(rng.randf_range(.25,.5),rng.randf_range(.18,.40),.45),Color("#53635d"),"Herbes")
		for z in [-20,-13,-7,7,14,21]:
			var base=Vector3(side*13.3,-.25,z)
			tube([base+Vector3(side*.8,.2,-.3),base+Vector3(0,.55,0),Vector3(side*11.9,.16,z+.35),Vector3(side*10.85,.015,z+.7)],[.21,.16,.10,.012],Color("#59604d"),"Herbes")
			tube([base+Vector3(0,.35,0),Vector3(side*12.3,.09,z-.65),Vector3(side*11.45,.015,z-.85)],[.13,.075,.009],Color("#4a5144"),"Herbes")
	# Planted banks along both short ends and matching corner patches.
	for sign_z in [-1,1]:
		for i in range(44):
			var x=-11.0+i*.5
			for band in range(3):
				var a=Vector3(x,[-.026,-.015,-.17][band],sign_z*([22.6,23.15,24.2][band]+.16*sin(x*1.3)))
				var b=Vector3(x+.5,a.y,sign_z*([22.6,23.15,24.2][band]+.16*sin((x+.5)*1.3)))
				var c=Vector3(x+.5,[-.015,-.17,-.78][band],sign_z*([23.15,24.2,26.8][band]+.24*sin((x+.5)*1.1)))
				var d=Vector3(x,c.y,sign_z*([23.15,24.2,26.8][band]+.24*sin(x*1.1)))
				ground_quad(a,b,c,d,palette[band],palette[band+1])
		for side in [-1,1]:
			ground_quad(Vector3(side*10.8,-.018,sign_z*23),Vector3(side*12.2,-.17,sign_z*23.6),Vector3(side*15,-.78,sign_z*26.8),Vector3(side*10.8,-.78,sign_z*26.8),palette[1],palette[3])
		for x in range(-10,11):
			grass_tuft(Vector3(x+rng.randf_range(-.2,.2),-.01,sign_z*rng.randf_range(23.1,23.7)),rng.randf_range(.16,.28))
			if x%3==0:rock(Vector3(x,-.06,sign_z*23.8),Vector3(.55,.35,.45),Color("#42614b"),"Herbes")

func grass_tuft(pos:Vector3,h:float):
	for i in range(5):
		var a=rng.randf_range(0,TAU)
		var dir=Vector3(cos(a),0,sin(a))
		var p=pos+dir*rng.randf_range(.02,.12)
		var tip=p+Vector3(0,h*rng.randf_range(.7,1.15),0)+dir*.10
		var width=Vector3(-dir.z,0,dir.x)*.045
		triangle("Herbes",p-width,p+width,tip,Color("#56744b"),false)

func fireflies():
	var shader=Shader.new()
	shader.code="""shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never;
varying float phase;
void vertex(){
    phase=INSTANCE_CUSTOM.x*6.28318;
    VERTEX.y+=sin(TIME*.6+phase)*.35;
    VERTEX.x+=cos(TIME*.35+phase)*.2;
}
void fragment(){
    float d=length(UV-vec2(.5))*2.;
    float halo=pow(max(0.,1.-d),2.5);
    ALBEDO=vec3(.24,.68,.72);
    ALPHA=halo*(.45+.25*sin(TIME*.9+phase));
}"""
	var material=ShaderMaterial.new();material.shader=shader
	var mesh=QuadMesh.new();mesh.size=Vector2(.55,.55);mesh.material=material
	var multi=MultiMesh.new();multi.transform_format=MultiMesh.TRANSFORM_3D;multi.use_custom_data=true;multi.mesh=mesh;multi.instance_count=72
	for i in range(72):
		var side=-1 if i%2==0 else 1
		var p=Vector3(side*rng.randf_range(13,20),rng.randf_range(.3,3.4),rng.randf_range(-29,22))
		multi.set_instance_transform(i,Transform3D(Basis(Vector3.RIGHT,deg_to_rad(-54)),p))
		multi.set_instance_custom_data(i,Color(rng.randf(),0,0,1))
	var motes=MultiMeshInstance3D.new();motes.name="Lucioles";motes.multimesh=multi
	motes.custom_aabb=AABB(Vector3(-24,-2,-34),Vector3(48,10,64))
	motes.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(motes)

func forest_mist():
	var shader=Shader.new()
	shader.code="""shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;
varying float phase;
void vertex(){phase=INSTANCE_CUSTOM.x*6.28318;}
void fragment(){
    vec2 p=UV*2.-1.;
    float feather=pow(max(0.,1.-p.x*p.x),2.)*pow(max(0.,1.-p.y*p.y),2.);
    float strands=.65+.2*sin(UV.x*19.+UV.y*8.+TIME*.13+phase);
    ALBEDO=vec3(.24,.40,.46);
    ALPHA=feather*strands*.28;
}"""
	var mat=ShaderMaterial.new();mat.shader=shader
	var mesh=QuadMesh.new();mesh.size=Vector2(7,1.8);mesh.material=mat
	var multi=MultiMesh.new();multi.transform_format=MultiMesh.TRANSFORM_3D;multi.use_custom_data=true;multi.mesh=mesh;multi.instance_count=19
	for i in range(19):
		var p:Vector3
		if i<5:p=Vector3(-24+i*12,.5,-29)
		else:p=Vector3((-1 if i%2==0 else 1)*18.5,.5,-26+((i-5)/2)*7)
		multi.set_instance_transform(i,Transform3D(Basis(Vector3.RIGHT,deg_to_rad(-54)),p))
		multi.set_instance_custom_data(i,Color(rng.randf(),0,0,1))
	var mist=MultiMeshInstance3D.new();mist.name="Brume_des_sous_bois";mist.multimesh=multi
	mist.cast_shadow=GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mist)
