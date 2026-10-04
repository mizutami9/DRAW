import av
from PIL import Image, ImageDraw

video = r"D:\04_tiktok_game\NICODROW\twitch_fixed.mp4"
output = r"C:\workSpace\PICO\.speaker_audit\ricochet_8_2_contact_sheet.jpg"
times = [56 * 60 + 38 + i * 0.25 for i in range(24)]

container = av.open(video)
stream = container.streams.video[0]
tiles = []
for second in times:
    container.seek(int(second / float(stream.time_base)), stream=stream, any_frame=False, backward=True)
    frame = next(f for f in container.decode(stream) if float(f.pts * stream.time_base) >= second)
    image = frame.to_image().convert("RGB")
    image = image.crop((0, 0, min(1530, image.width), min(900, image.height)))
    image.thumbnail((480, 270))
    tile = Image.new("RGB", (480, 292), "white")
    tile.paste(image, ((480 - image.width) // 2, 22))
    label = f"{int(second)//3600:02d}:{int(second)%3600//60:02d}:{second%60:04.1f}"
    ImageDraw.Draw(tile).text((8, 4), label, fill="black")
    tiles.append(tile)

sheet = Image.new("RGB", (480 * 4, 292 * 6), "#dddddd")
for index, tile in enumerate(tiles):
    sheet.paste(tile, ((index % 4) * 480, (index // 4) * 292))
sheet.save(output, quality=92)
