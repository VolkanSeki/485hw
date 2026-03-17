import random


def generate_2d_complex_maze_wide(width, height):
    # Hücre bazlı mantık için boyutların tek sayı olması gerekir [cite: 53]
    width = width if width % 2 != 0 else width + 1
    height = height if height % 2 != 0 else height + 1

    # 1: Duvar, 0: Yol [cite: 53]
    maze = [[1 for _ in range(height)] for _ in range(width)]

    # Labirent oluşturma algoritması (Wilson's-like/Recursive Backtracker)
    stack = [(1, 1)]
    maze[1][1] = 0
    visited = {(1, 1)}

    while stack:
        cx, cy = stack[-1]
        neighbors = []
        # 2 birim atlayarak komşuları kontrol et (duvar ve yol ayrımı için)
        for dx, dy in [(0, 2), (0, -2), (2, 0), (-2, 0)]:
            nx, ny = cx + dx, cy + dy
            if 1 <= nx < width - 1 and 1 <= ny < height - 1 and (nx, ny) not in visited:
                neighbors.append((nx, ny))

        if neighbors:
            nx, ny = random.choice(neighbors)
            # Aradaki duvarı kaldır ve yeni hücreyi yol yap
            maze[(cx + nx) // 2][(cy + ny) // 2] = 0
            maze[nx][ny] = 0
            visited.add((nx, ny))
            stack.append((nx, ny))
        else:
            stack.pop()

    # Kenarlarda 3 adet rastgele çıkış (kapı/başlangıç için) oluştur [cite: 12]
    exits_created = 0
    while exits_created < 3:
        edge = random.choice(['top', 'bottom', 'left', 'right'])
        if edge == 'top':
            rx, ry = random.randrange(1, width - 1), 0
        elif edge == 'bottom':
            rx, ry = random.randrange(1, width - 1), height - 1
        elif edge == 'left':
            rx, ry = 0, random.randrange(1, height - 1)
        else:
            rx, ry = width - 1, random.randrange(1, height - 1)

        if maze[rx][ry] == 1:
            maze[rx][ry] = 0
            exits_created += 1

    # GENİŞLETİLMİŞ ÇIKTI AYARLARI
    wall_height = 2  # Ödevde belirtilen standart duvar yüksekliği
    scale = 2  # Koridorları ve duvarları 2 katına çıkaran çarpan

    for x in range(width):
        for y in range(height):
            if maze[x][y] == 1:
                # Koordinatları 2 ile çarparak merkezleri uzaklaştırıyoruz
                pos_x = x * scale
                pos_z = y * scale

                # Format: (Pozisyon) (Boyut)
                # Boyutu (2, 2, 2) yaparak duvarların birbirine bitişik,
                # yolların ise 2 birim genişliğinde olmasını sağlıyoruz
                print(f"({pos_x}, {wall_height / 2}, {pos_z}) ({scale}, {wall_height}, {scale})")


# 21x21 boyutunda (genişletilmiş haliyle 42x42 alan kaplar) labirent üret
generate_2d_complex_maze_wide(21, 21)