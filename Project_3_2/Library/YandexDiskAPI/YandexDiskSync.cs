using System.Text.Json;
using System.Text.RegularExpressions;

namespace Library.YandexDiskAPI
{
    /// <summary>
    /// Исключение для ошибок аутентификации.
    /// </summary>
    public class AuthException : Exception
    {
        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="AuthException"/> с указанным сообщением об ошибке.
        /// </summary>
        /// <param name="message">Сообщение об ошибке.</param>
        public AuthException(string message) : base(message) { }
    }

    /// <summary>
    /// Исключение, возникающее при неверном коде авторизации.
    /// </summary>
    public class InvalidCodeException : AuthException
    {
        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="InvalidCodeException"/> с указанным сообщением об ошибке.
        /// </summary>
        /// <param name="message">Сообщение об ошибке.</param>
        public InvalidCodeException(string message) : base(message) { }
    }

    /// <summary>
    /// Исключение, возникающее при ошибке запроса токена.
    /// </summary>
    public class TokenRequestException : AuthException
    {
        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="TokenRequestException"/> с указанным сообщением об ошибке.
        /// </summary>
        /// <param name="message">Сообщение об ошибке.</param>
        public TokenRequestException(string message) : base(message) { }
    }

    /// <summary>
    /// Исключение, возникающее при ошибке в ответе на запрос токена.
    /// </summary>
    public class TokenResponseException : AuthException
    {
        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="TokenResponseException"/> с указанным сообщением об ошибке.
        /// </summary>
        /// <param name="message">Сообщение об ошибке.</param>
        public TokenResponseException(string message) : base(message) { }
    }

    /// <summary>
    /// Предоставляет функционал для работы с Яндекс.Диском через API.
    /// </summary>
    public class YandexDiskService
    {
        /// <summary>
        /// URL для авторизации через OAuth.
        /// </summary>
        private const string AuthUrl = "https://oauth.yandex.ru/authorize";

        /// <summary>
        /// URL для получения токена доступа.
        /// </summary>
        private const string TokenUrl = "https://oauth.yandex.ru/token";

        /// <summary>
        /// Базовый URL для API Яндекс.Диска.
        /// </summary>
        private const string ApiUrl = "https://cloud-api.yandex.net/v1/disk";

        /// <summary>
        /// Идентификатор клиента.
        /// </summary>
        private readonly string _clientId;

        /// <summary>
        /// Секретный ключ клиента.
        /// </summary>
        private readonly string _clientSecret;

        /// <summary>
        /// Токен доступа для API.
        /// </summary>
        private string _accessToken;

        /// <summary>
        /// Инициализирует новый экземпляр класса с указанным идентификатором и секретным ключом.
        /// </summary>
        /// <param name="clientId">Идентификатор клиента.</param>
        /// <param name="clientSecret">Секретный ключ клиента.</param>
        public YandexDiskService(string clientId, string clientSecret)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
        }

        /// <summary>
        /// Происходит аунтефикация пользователя и получение токена доступа.
        /// </summary>
        /// <exception cref="InvalidCodeException">Выбрасывается, если код авторизации неверен.</exception>
        /// <exception cref="TokenRequestException">Выбрасывается, если запрос токена завершился ошибкой.</exception>
        /// <exception cref="TokenResponseException">Выбрасывается, если токен доступа не был получен.</exception>
        public async Task Authenticate()
        {
            Console.WriteLine("=== Яндекс.OAuth Аутентификация ===");

            // Получение кода авторизации.
            var authRequestUrl = $"{AuthUrl}?response_type=code&client_id={_clientId}&scope=cloud_api:disk.app_folder";
            Console.WriteLine($"1. Перейдите по ссылке: {authRequestUrl}");
            Console.WriteLine("2. Авторизуйтесь под своим аккаунтом Яндекс.");
            Console.WriteLine("3. Скопируйте код подтверждения.");

            string code;
            while (true)
            {
                Console.Write("Введите полученный код: ");
                code = Console.ReadLine()!;

                // Проверка на пустой ввод.
                if (string.IsNullOrWhiteSpace(code))
                {
                    Console.WriteLine("[red]Ошибка: Код не может быть пустым. Попробуйте снова.[/]");
                    continue; // Повторяем запрос.
                }

                // Проверка на минимальную длину кода.
                if (code.Length < 10)
                {
                    Console.WriteLine("[red]Ошибка: Код должен содержать не менее 10 символов. Попробуйте снова.[/]");
                    continue; // Повторяем запрос.
                }

                // Проверка на максимальную длину кода (если требуется).
                if (code.Length > 20) // Пример: максимальная длина 20 символов.
                {
                    Console.WriteLine("[red]Ошибка: Код не должен превышать 20 символов. Попробуйте снова.[/]");
                    continue; // Повторяем запрос.
                }

                // Проверка формата кода с использованием регулярного выражения (если требуется).
                if (!Regex.IsMatch(code, @"^[a-zA-Z0-9]{10,20}$")) // Пример: только буквы и цифры, длина от 10 до 20.
                {
                    Console.WriteLine("[red]Ошибка: Код должен состоять только из букв и цифр. Попробуйте снова.[/]");
                    continue; // Повторяем запрос.
                }

                // Если все проверки пройдены, выходим из цикла.
                break;
            }

            // Получение access token.
            using var client = new HttpClient();
            var content = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("client_secret", _clientSecret)
        });

            Console.WriteLine("Получение токена доступа...");
            var response = await client.PostAsync(TokenUrl, content);

            // Проверка статуса ответа.
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new TokenRequestException($"Ошибка при получении токена: {response.StatusCode}. Детали: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);

            // Проверка наличия токена в ответе.
            if (string.IsNullOrEmpty(tokenResponse?.access_token))
            {
                throw new TokenResponseException("Токен доступа не был получен.");
            }

            _accessToken = tokenResponse.access_token;
            Console.WriteLine("Аутентификация успешно завершена!");
        }

        /// <summary>
        /// Загружает файл на Яндекс.Диск.
        /// </summary>
        /// <param name="localPath">Локальный путь к файлу на диске.</param>
        /// <param name="remotePath">Удаленный путь на Яндекс.Диске.</param>
        public async Task UploadFile(string localPath, string remotePath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"OAuth {_accessToken}");
            client.DefaultRequestHeaders.AcceptCharset.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("utf-8"));

            // Кодирование пути для URL.
            var encodedPath = Uri.EscapeDataString(remotePath);

            // Формирование корректного URL.
            var requestUrl = $"{ApiUrl}/resources/upload?path={encodedPath}&overwrite=true";

            // Получение URL для загрузки.
            var uploadUrlResponse = await client.GetAsync(requestUrl);

            // Проверка ответа.
            if (!uploadUrlResponse.IsSuccessStatusCode)
            {
                var error = await uploadUrlResponse.Content.ReadAsStringAsync();
            }

            var uploadData = await uploadUrlResponse.Content.ReadAsStringAsync();
            var uploadInfo = JsonSerializer.Deserialize<UploadInfo>(uploadData);

            // Чтение файла с UTF-8.
            var fileContent = await File.ReadAllBytesAsync(localPath);

            // Загрузка файла.
            using var content = new ByteArrayContent(fileContent);
            var response = await client.PutAsync(uploadInfo.href, content);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Скачивает файл с Яндекс.Диска.
        /// </summary>
        /// <param name="remotePath">Удаленный путь на Яндекс.Диске.</param>
        /// <param name="localPath">Локальный путь для сохранения файла.</param>
        /// <exception cref="Exception">Выбрасывается, если произошла ошибка при скачивании файла.</exception>
        public async Task DownloadFile(string remotePath, string localPath)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"OAuth {_accessToken}");
            client.DefaultRequestHeaders.AcceptCharset.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("utf-8"));

            // Кодирование пути для URL.
            var encodedPath = Uri.EscapeDataString(remotePath);

            // Формирование корректного URL.
            var requestUrl = $"{ApiUrl}/resources/download?path={encodedPath}";

            var response = await client.GetAsync(requestUrl);

            // Проверка ответа.
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"API Error: {error}");
            }

            var downloadData = await response.Content.ReadAsStringAsync();
            var downloadInfo = JsonSerializer.Deserialize<DownloadInfo>(downloadData);

            // Скачивание файла.
            var fileResponse = await client.GetAsync(downloadInfo.href);
            await using var fileStream = File.Create(localPath);
            await fileResponse.Content.CopyToAsync(fileStream);
        }

        /// <summary>
        /// Вспомогательный класс для десериализации ответа с токеном.
        /// </summary>
        private class TokenResponse
        {
            /// <summary>
            /// Токен доступа.
            /// </summary>
            public string access_token { get; set; }
        }

        /// <summary>
        /// Вспомогательный класс для десериализации ответа с URL для загрузки.
        /// </summary>
        private class UploadInfo
        {
            /// <summary>
            /// URL для загрузки файла.
            /// </summary>
            public string? href { get; set; }
        }

        /// <summary>
        /// Вспомогательный класс для десериализации ответа с URL для скачивания.
        /// </summary>
        private class DownloadInfo
        {
            /// <summary>
            /// URL для скачивания файла.
            /// </summary>
            public string? href { get; set; }
        }
    }



}
