# Лабораторная работа №3: Контейнеризация веб-сервера в Docker и раздача сжатых файлов со стороны Nginx

Студент: Гоман Глеб

Репозиторий: https://github.com/Henieknn/ARPO_1

## Цель работы

Контейнеризация WebGL-версии игры в Docker-образ на базе Nginx (Alpine Linux): настройка корректной раздачи сжатых Gzip-файлов Unity 6 через HTTP-заголовок Content-Encoding, локальное тестирование контейнера и интеграция автоматической сборки образа в CI/CD-пайплайн с публикацией в GitHub Container Registry (GHCR).

## Ход работы

### Шаг 1: Конфигурация сжатия в Unity 6 и локальная пересборка

Запустил редактор Unity 6 и открыл проект 2D-платформера. Перешёл в File -> Build Profiles -> кнопка Player Settings -> вкладка Player -> настройки WebGL (иконка планеты). В Publishing Settings изменил параметр Compression Format со значения Disabled обратно на **Gzip**. Критически важно: снял галочку с пункта Decompression Fallback (Резервная декомпрессия) — задача лабораторной работы настроить веб-сервер «по-честному», без JS-распаковщиков на стороне клиента. Сохранил изменения и закрыл редактор.

Проверка фактических значений в ProjectSettings/ProjectSettings.asset:

```
webGLCompressionFormat: 1   (Gzip)
webGLDecompressionFallback: 0
```

Пересобрал проект через терминал командой из ЛР №1:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -executeMethod BuildManager.BuildWebGL -quit -logFile build_webgl.log
```

В логе: `[CI/CD] УСПЕХ! WebGL билд успешно создан. Время сборки: 1210,02 сек. Размер: 20395850 байт.` Сжатие Gzip уменьшило билд с 65,2 МБ (ЛР №1, без сжатия) до 20,4 МБ — примерно в 3 раза. В папке `Builds/WebGL/Build/` файлы изменили расширения: `WebGL.framework.js.gz`, `WebGL.wasm.gz`, `WebGL.data.gz` — структура сжатой сборки Unity 6.

![Шаг 1](docs/30.jpg)

### Шаг 2: Создание конфигурации веб-сервера nginx.conf

Браузеры блокируют запуск `.gz`-архивов Unity с ошибкой «Unable to parse…», если сервер не сообщает заголовок `Content-Encoding: gzip`. Создал в корне проекта файл `nginx.conf`, который заставляет Nginx принудительно подставлять правильные типы контента и заголовки сжатия: отдельные location-блоки для `.js.gz` (application/javascript), `.wasm.gz` (application/wasm) и `.data.gz` (application/octet-stream) — каждый с директивой `add_header Content-Encoding gzip;`.

![Шаг 2](docs/31.jpg)

### Шаг 3: Создание файла Dockerfile с подменой слоев

Создал в корне проекта `Dockerfile` (без расширения): базовый образ `nginx:alpine`, удаление дефолтной конфигурации `default.conf`, копирование кастомного `nginx.conf` в `/etc/nginx/conf.d/`, очистка стандартной веб-папки и копирование туда сжатой игры из `Builds/WebGL/`, объявление порта 80 и запуск Nginx в режиме демона (`daemon off;`).

![Шаг 3](docs/32.jpg)

### Шаг 4: Локальное тестирование контейнера

Собрал локальный образ и запустил контейнер с пробросом порта:

```bash
docker build -t platformer-compressed:v1 .
docker run -d -p 8080:80 --name unity_compressed_container platformer-compressed:v1
```

Открыл http://localhost:8080 — игра успешно загрузилась. В инструментах разработчика (F12 -> Network, Ctrl + F5) у файла `.wasm.gz` во вкладке Headers убедился в наличии строки `Content-Encoding: gzip` — правила nginx.conf работают. Остановил тестовый контейнер:

```bash
docker stop unity_compressed_container && docker rm unity_compressed_container
```

![Шаг 4](docs/33.jpg)

### Шаг 5: Модернизация облачного пайплайна в GitHub Actions

Папка Builds/ отсекается файлом .gitignore, поэтому облачный пайплайн научил создавать временные «фейковые» файлы-заглушки с расширением .gz — это позволяет Docker-инструментам успешно завершить сборку образа до подключения облачного Unity-компилятора в ЛР №5. Добавил в `.github/workflows/main.yml` третий этап `build_docker_image`:

- `needs: sanity_check` — образ собирается только после проверки структуры проекта;
- `permissions: contents: read, packages: write` — разрешение публиковать пакеты в GHCR;
- шаг `Create Compressed Placeholders for Docker` — генерация заглушек `game.framework.js.gz`, `game.wasm.gz`, `game.data.gz`, если папка Builds/WebGL/Build пуста;
- `docker/login-action@v3` — вход в реестр ghcr.io с системным токеном `GITHUB_TOKEN`;
- `docker/build-push-action@v5` — сборка образа из Dockerfile и публикация под двумя тегами: `ghcr.io/henieknn/platformer-game:latest` и `ghcr.io/henieknn/platformer-game:${{ github.sha }}` (привязка к коммиту).

### Шаг 6: Отправка изменений в облако

Отправил конфигурацию nginx.conf, Dockerfile и обновлённый пайплайн на GitHub в ветке LR3 через Pull Request:

```bash
git checkout -b LR3
git add nginx.conf Dockerfile .github/workflows/main.yml
git commit -m "feat: added Docker containerization with nginx gzip serving and GHCR pipeline stage"
git push origin LR3
```

### Шаг 7: Проверка результатов и отчёт

- Файлы Dockerfile, nginx.conf и обновлённый main.yml присутствуют в основном репозитории.
- Во вкладке Actions все три задачи (sanity_check, mirror_repo, build_docker_image) прошли со статусом Passed.
- Во вкладке Packages профиля GitHub появился готовый пакет platformer-game — переносимый образ (веб-сервер Nginx + сжатая игра внутри) под тегами latest и github.sha.
- Созданы файлы LR1.md, LR2.md и LR3.md с шагами выполнения работ, в README.md размещена таблица со ссылками на отчёты.

![Шаг 7](docs/34.jpg)

## Контрольные вопросы

**1. Какую техническую проблему решает добавление строки add_header Content-Encoding gzip; в конфигурацию Nginx?**

Unity 6 собирает WebGL-игру в предсжатые Gzip-архивы (.js.gz, .wasm.gz, .data.gz). Браузер, получив такой файл без заголовка Content-Encoding: gzip, пытается выполнить его как есть и падает с ошибкой «Unable to parse…». Строка add_header сообщает браузеру, что тело ответа сжато Gzip и его нужно распаковать перед исполнением — без этого заголовка сжатая игра в браузере не запускается.

**2. Почему в рамках текущей лабораторной работы в облачном пайплайне пришлось использовать генерацию временных файлов через команду touch Builds/WebGL/Build/game.wasm.gz?**

Папка Builds/ исключена из Git правилом .gitignore (сборочные артефакты не версионируются), поэтому на облачном раннере GitHub Actions её просто нет. При этом шаг COPY Builds/WebGL/ в Dockerfile упадёт, если папки/файлов не существует. Заглушки через touch создают временную структуру, чтобы сборка Docker-образа прошла успешно до подключения настоящего облачного Unity-компилятора в ЛР №5.

**3. В какую директорию внутри контейнера Nginx необходимо копировать финальные скомпилированные файлы WebGL-игры согласно официальной документации образа?**

В `/usr/share/nginx/html/` — стандартную корневую веб-директорию официального образа Nginx, которая раздаётся сервером по умолчанию. В Dockerfile это делается инструкцией `COPY Builds/WebGL/ /usr/share/nginx/html/` после очистки дефолтного содержимого.

**4. Зачем публиковать готовый образ в реестр GHCR под двумя тегами (latest и github.sha)?**

Тег latest — указатель на самую свежую версию: команды вроде `docker run ghcr.io/henieknn/platformer-game:latest` всегда забирают последний успешный билд, удобно для быстрого запуска и тестирования. Тег с хэшем коммита (${{ github.sha }}) — неизменяемая привязка к конкретной ревизии кода: позволяет в любой момент откатиться или запустить именно ту сборку, которая соответствовала определённому коммиту, и упрощает аудит (какой коммит — такой образ).

## Выводы

В ходе работы освоил контейнеризацию веб-сервера для WebGL-игры: настроил корректную раздачу предсжатых Gzip-файлов Unity 6 через кастомный nginx.conf с заголовками Content-Encoding, собрал переносимый Docker-образ на базе nginx:alpine с игрой внутри, провёл его локальное тестирование с проверкой HTTP-заголовков, а также интегрировал автоматическую сборку и публикацию образа в реестр GHCR в CI/CD-пайплайн с тегами latest и github.sha.
