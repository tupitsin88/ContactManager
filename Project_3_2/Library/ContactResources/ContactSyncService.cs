using Library.YandexDiskAPI;
using System.Text.RegularExpressions;

namespace Library.ContactResources
{
    public class ContactSyncService
    {
        /// <summary>
        /// Путь локального файла на диске.
        /// </summary>
        private string LocalFilePath = "contacts.txt";

        /// <summary>
        /// Путь к файлу на Яндекс.Диске.
        /// </summary>
        private const string RemoteFilePath = "app:/contacts.txt";

        /// <summary>
        /// Параметр для работы с Яндекс диском.
        /// </summary>
        private readonly YandexDiskService _yandexDisk;

        /// <summary>
        /// Инициализирует поле _yandexDisk.
        /// </summary>
        /// <param name="clientId">ID клиента.</param>
        /// <param name="clientSecret">Секретный ID клиента.</param>
        public ContactSyncService(string clientId, string clientSecret)
        {
            _yandexDisk = new YandexDiskService(clientId, clientSecret);
        }

        /// <summary>
        /// Метод для получения Токена API.
        /// </summary>
        /// <remarks>
        /// Метод вызывает аутентификацию через Яндекс.OAuth и обрабатывает возможные исключения,
        /// связанные с неверным кодом авторизации, ошибками запроса токена и обработки ответа.
        /// </remarks>
        public async Task InitializeAuth()
        {
            await _yandexDisk.Authenticate();
            Console.WriteLine("Токен успешно получен!");

        }

        /// <summary>
        /// Асинхронный метод, загружающий данные в облако.
        /// </summary>
        /// <param name="contacts"></param>
        public async Task SyncToCloud(List<Contact> contacts)
        {
            // Сериализация контактов в текстовый формат
            var lines = contacts.Select(c => $"[{c.Id}] [{c.FirstName}] [{c.SecondName}] [{c.Phone}] [{c.Email}]");
            if (!File.Exists(LocalFilePath))
            {
                Console.WriteLine("Нет файла на диске.");
            }
            // Сначал записываем в локальный файл, далее
            await File.WriteAllLinesAsync(LocalFilePath, lines);

            // Загрузка на Яндекс.Диск.
            await _yandexDisk.UploadFile(LocalFilePath, RemoteFilePath);
        }

        /// <summary>
        /// Асинхронный метод, загружающий данные из облака.
        /// </summary>
        /// <returns>Список импортированных контактов из облака.</returns>
        /// <exception cref="FileNotFoundException">Выбрасывается, если файл не найден.</exception>
        /// 
        public async Task<List<Contact>> SyncFromCloud()
        {
            try
            {
                // Скачивание с Яндекс.Диска.
                await _yandexDisk.DownloadFile(RemoteFilePath, LocalFilePath);

                // Чтение данных из текстового файла.
                var lines = await File.ReadAllLinesAsync(LocalFilePath);

                // Регулярное выражение для проверки формата.
                var pattern = @"\[(\d+)\]\s+\[([A-ZА-Я][а-я]+)]\s+\[([A-ZА-Я][а-я]+)]\s+\[(\+7\d{10})]\s+\[([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})]";
                var regex = new Regex(pattern);

                // Десериализация контактов.
                var contacts = new List<Contact>();
                foreach (var line in lines)
                {
                    // Проверка строки на соответствие регулярному выражению.
                    var match = regex.Match(line);
                    if (!match.Success)
                    {
                        Console.WriteLine($"Ошибка: Некорректный формат строки: {line}");
                        continue;
                    }

                    // Извлечение данных из групп регулярного выражения.
                    var contact = new Contact
                    {
                        Id = int.Parse(match.Groups[1].Value), // ID
                        FirstName = match.Groups[2].Value,     // Имя
                        SecondName = match.Groups[3].Value,    // Фамилия
                        Phone = match.Groups[4].Value,         // Телефон
                        Email = match.Groups[5].Value          // Email
                    };
                    contacts.Add(contact);
                }
                return contacts;
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Ошибка: Файл не найден.");
                return new List<Contact>();
            }
        }
    }
}
