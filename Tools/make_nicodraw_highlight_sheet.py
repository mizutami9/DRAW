import av
from PIL import Image, ImageDraw

video = r"D:\04_tiktok_game\NICODROW\twitch_fixed.mp4"
output = r"C:\workSpace\PICO\.speaker_audit\highlight_sheet.jpg"
times = [499, 1091, 1547, 2849, 3015, 3393, 3643, 4198, 4906, 5588, 6310, 6884]

container = av.open(video)
stream = container.streams.video[0]
frames = []
for second in times:
    container.seek(int(second / float(stream.time_base)), stream=stream, any_frame=False, backward=True)
    frame = next(f for f in container.decode(stream) if float(f.pts * stream.time_base) >= second)
    image = frame.to_image().convert("RGB")
    image.thumbnail((480, 270))
    canvas = Image.new("RGB", (480, 300), "white")
    canvas.paste(image, ((480 - image.width) // 2, 30))
    label = f"{second // 3600:02d}:{second % 3600 // 60:02d}:{second % 60:02d}"
    ImageDraw.Draw(canvas).text((10, 8), label, fill="black")
    frames.append(canvas)

sheet = Image.new("RGB", (480 * 3, 300 * 4), "#dddddd")
for index, image in enumerate(frames):
    sheet.paste(image, ((index % 3) * 480, (index // 3) * 300))
sheet.save(output, quality=90)
