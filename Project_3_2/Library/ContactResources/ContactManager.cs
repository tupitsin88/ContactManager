using Spectre.Console;
using System.Text.RegularExpressions;
using Library.YandexDiskAPI;

namespace Library.ContactResources
{
    public class ContactManager
    {
        /// <summary>
        /// Отсортированный Set использованных уникальных значений ID.
        /// </summary>
        private readonly SortedSet<int> _usedIds = [];

        /// <summary>
        /// Отсортированный Set освобожденных уникальных значений ID.
        /// </summary>
        private readonly SortedSet<int> _freeIds = [];

        /// <summary>
        /// Список контактов.
        /// </summary>
        private List<Contact> _contacts = [];

        /// <summary>
        /// Асинхронный метод для запуска меню синхронизации контактов.
        /// </summary>
        /// <remarks>
        /// Метод предоставляет пользователю возможность загружать, скачивать контакты
        /// и выходить из программы.
        /// </remarks>
        /// <exception cref="InvalidCodeException">Выбрасывается, если введен неверный или пустой код авторизации.</exception>
        /// <exception cref="TokenRequestException">Выбрасывается, если произошла ошибка при запросе токена доступа.</exception>
        /// <exception cref="TokenResponseException">Выбрасывается, если токен доступа не был получен из ответа сервера.</exception>

        public async Task RunSyncMenu()
        {
            // Клиентские данные для аутентификации в Yandex.Disk.
            const string clientId = "ef158d87214045b88b39d3fdf93662cf";
            const string clientSecret = "1b24766711334a4084090cd6a02511ab";

            var syncService = new ContactSyncService(clientId, clientSecret);

            AnsiConsole.MarkupLine("[cyan1]=== Синхронизация контактов с Yandex.Disk ===[/]\n");

            // Первичная аутентификация.
            try
            {
                await syncService.InitializeAuth();
            }
            catch (InvalidCodeException ex)
            {
                Console.WriteLine($"Ошибка ввода кода: {ex.Message}");
                return;
            }
            catch (TokenRequestException ex)
            {
                Console.WriteLine($"Ошибка запроса токена: {ex.Message}");
                return;
            }
            catch (TokenResponseException ex)
            {
                Console.WriteLine($"Ошибка обработки ответа: {ex.Message}");
                return;
            }

            while (true)
            {
                // Отображение главного меню.
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan1]Выберите действие:[/]")
                        .AddChoices(
                        [
                    "Загрузить контакты в облако",
                    "Скачать контакты из облака",
                    "Выход"
                        ])
                );

                switch (choice)
                {
                    case "Загрузить контакты в облако":
                        try
                        {
                            await HandleUpload(syncService);
                        }
                        catch (IOException ex)
                        {
                            AnsiConsole.MarkupLine($"[bold red]Ошибка ввода-вывода при загрузке: {ex.Message}[/]");
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            AnsiConsole.MarkupLine($"[bold red]Недостаточно прав доступа: {ex.Message}[/]");
                        }
                        break;

                    case "Скачать контакты из облака":
                        try
                        {
                            await HandleDownload(syncService, _contacts);
                        }
                        catch (FileNotFoundException ex)
                        {
                            AnsiConsole.MarkupLine($"[bold red]Файл не найден: {ex.Message}[/]");
                        }
                        break;

                    case "Выход":
                        AnsiConsole.MarkupLine("[bold red]Выход...[/]");
                        return;
                }

                AnsiConsole.WriteLine("Нажмите любую клавишу для продолжения...");
                Console.ReadKey();
                AnsiConsole.Clear();
            }
        }


        /// <summary>
        /// Метод для проверки существования уникального номера контакта.
        /// </summary>
        /// <param name="id">Целочисленное значение.</param>
        /// <returns>True, если ID занят, иначе False</returns>
        private bool IdExists(int id) => _usedIds.Contains(id);

        /// <summary>
        /// Парсит входную строку и извлекает контакты в соответствии с заданным форматом.
        /// </summary>
        /// <param name="input">Данные из входного файла.</param>
        /// <remarks>
        /// Метод использует регулярное выражение для проверки строки на соответсвие и извлеченет данных контактов.
        /// Каждый контакт должен соответствовать следующему формату:
        /// [ID] [FirstName] [SecondName] [Phone] [Email]
        /// Пример: [1] [Иван] [Иванов] [+79123456789] [ivan@example.com]
        /// Если контакт с указанным ID уже существует, он не будет добавлен повторно.
        /// </remarks>
        public void ParseContacts(string input)
        {
            // Регулярное выражение для парсинга строки контактов
            // 1. ID (число в квадратных скобках)
            // 2. FirstName (имя, начинается с заглавной буквы)
            // 3. SecondName (фамилия, начинается с заглавной буквы)
            // 4. Phone (телефон в формате +7XXXXXXXXXX)
            // 5. Email (электронная почта в стандартном формате)
            string pattern = @"\[(\d+)\]\s+\[([A-ZА-Я][а-я]+)]\s+\[([A-ZА-Я][а-я]+)]\s+\[(\+7\d{10})]\s+\[([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})]";
            Regex regex = new(pattern, RegexOptions.Multiline);

            // Поиск всех совпадений с регулярным выражением.
            MatchCollection matches = regex.Matches(input);

            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    // Создание нового контакта из входных данных.
                    Contact contact = new()
                    {
                        Id = int.Parse(match.Groups[1].Value),
                        FirstName = match.Groups[2].Value,
                        SecondName = match.Groups[3].Value,
                        Phone = match.Groups[4].Value,
                        Email = match.Groups[5].Value
                    };

                    // Проверка, существует ли контакт с таким ID.
                    if (!IdExists(contact.Id))
                    {
                        // Добавление контакта в список и отметка ID как использованного
                        _contacts.Add(contact);
                        _usedIds.Add(contact.Id);
                    }
                }
            }
        }

        /// <summary>
        /// Обрабатывает процесс загрузки контактов в облако.
        /// </summary>
        /// <param name="syncService">Сервис синхронизации, используемый для загрузки контактов.</param>
        private async Task HandleUpload(ContactSyncService syncService)
        {
            AnsiConsole.MarkupLine("[cyan1]Загрузка контактов в облако...[/]");
            await syncService.SyncToCloud(_contacts);
            AnsiConsole.MarkupLine("[green]Контакты успешно загружены в облако![/]");
        }

        /// <summary>
        /// Обрабатывает скачивание контактов из облака и обновляет список контактов.
        /// </summary>
        /// <param name="syncService">Сервис синхронизации для скачивания контактов.</param>
        /// <param name="contacts">Список контактов, который нужно обновить.</param>
        private static async Task HandleDownload(ContactSyncService syncService, List<Contact> contacts)
        {
            AnsiConsole.MarkupLine("[cyan1]Скачивание контактов из облака...[/]");

            // Скачиваем контакты из облака.
            List<Contact> downloadedContacts = await syncService.SyncFromCloud();

            // Очищаем текущий список контактов и добавляем скачанные.
            contacts.Clear();
            contacts.AddRange(downloadedContacts);

            AnsiConsole.MarkupLine("[green]Контакты успешно скачаны из облака![/]");
        }

        /// <summary>
        /// Записывает в файл список контактов на момент завершения пользователем программы.
        /// </summary>
        /// <param name="filePath">Путь к исходному файлу.</param>
        public void WriteDataToFile(string filePath)
        {
            using (StreamWriter writer = new(filePath))
            {
                foreach (var contact in _contacts)
                {
                    writer.WriteLine($"[{contact.Id}] [{contact.FirstName}] [{contact.SecondName}] [{contact.Phone}] [{contact.Email}]");
                }
            }

            AnsiConsole.MarkupLine($"[green]Поле успешно сохранены в файл.[/]");
            Environment.Exit(0);
        }

        /// <summary>
        /// Создает Breakdown Chart по именам и выводит его.
        /// </summary>
        public void CreateAndShowContactsNameBreakdown()
        {
            if (_contacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Список контактов пуст.[/]");
                return;
            }

            // Группируем контакты по имени и считаем количество.
            var nameCounts = _contacts
                .GroupBy(c => c.FirstName)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(10) // Ограничиваем до топ-10 имен.
                .ToList();

            int totalContacts = _contacts.Count;

            // Фиксированный список из 10 статичных цветов.
            var staticColors = new List<Color>
            {
                Color.Red,
                Color.Purple,
                Color.Blue,
                Color.Green,
                Color.Magenta1,
                Color.Cyan1,
                Color.Orange1,
                Color.Pink1,
                Color.Gold1,
                Color.White
            };

            // Создаем Breakdown Chart.
            var chart = new BreakdownChart()
                .Width(60) // Фиксированная ширина диаграммы.
                .ShowPercentage() // Показываем только проценты.
                .AddItems(nameCounts.Select((с, index) =>
                {
                    double percentage = Math.Round((double)с.Count / totalContacts * 100, 1);
                    return new BreakdownChartItem(
                        с.Name, // Показываем только имя.
                        percentage,
                        staticColors[index % staticColors.Count] // Присваиваем цвет из списка.
                    );
                }));

            // Отображаем заголовок и диаграмму
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[cyan1]Breakdown Chart контактов по именам[/]\n");
            AnsiConsole.Write(chart); // Выводим диаграмму
        }

        /// <summary>
        /// Присваивает или обновляет дату рождения выбранного контакта.
        /// </summary>
        /// <param name="contact">Контакт.</param>
        private void AddOrUpdateDateOfBirth(Contact contact)
        {

            if (_contacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Список контактов пуст.[/]");
                return;
            }

            AnsiConsole.MarkupLine("[cyan1]Введите дату рождения контакта:[/]");
            AnsiConsole.MarkupLine("[grey](Формат: dd.mm, например, 15.06)[/]");

            while (true)
            {
                string input = AnsiConsole.Ask<string>("Дата рождения:");

                if (string.IsNullOrWhiteSpace(input))
                {
                    AnsiConsole.MarkupLine("[red]Дата рождения не указана. Будет установлено значение 'Неизвестно'.[/]");
                    contact.SetDateOfBirth(null);
                    break;
                }

                // Устанавливаем дату рождения
                contact.SetDateOfBirth(input);

                // Если дата установлена успешно, выходим из цикла
                if (contact.DateOfBirth != "Неизвестно")
                {
                    break;
                }
            }

            AnsiConsole.MarkupLine($"[green]Дата рождения успешно установлена: {contact.DateOfBirth}[/]");
        }

        /// <summary>
        /// Позволяет пользователю выбрать контакт для обновления или добавления даты рождения.
        /// </summary>
        /// <remarks>
        /// Метод отображает список контактов и позволяет пользователю выбрать один из них.
        /// После выбора контакта вызывается метод <see cref="AddOrUpdateDateOfBirth"/> для добавления или обновления даты рождения.
        /// </remarks>
        public void ChooseContactForUpdate()
        {
            if (_contacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Список контактов пуст. Нечего редактировать.[/]");
                return;
            }

            // Создаем меню выбора контакта.
            var contactChoices = _contacts.Select(c => $"{c.Id}: {c.FirstName} {c.SecondName} {c.Phone} {c.Email}").ToList();
            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan1]Выберите контакт:[/]")
                    .PageSize(10)
                    .AddChoices(contactChoices)
            );

            // Находим выбранный контакт.
            int contactId = int.Parse(selected.Split(':')[0]);
            var contact = _contacts.FirstOrDefault(c => c.Id == contactId);
            if (contact == null)
            {
                AnsiConsole.MarkupLine($"[red]Контакт с ID {contactId} не найден.[/]");
                return;
            }

            // Добавляем или обновляем дату рождения.
            AddOrUpdateDateOfBirth(contact);

            AnsiConsole.MarkupLine($"[green]Дата рождения успешно добавлена для контакта: {contact.FirstName} {contact.SecondName}[/]");
        }


        /// <summary>
        /// Выполняет поиск контактов по выбранному полю и заданному запросу.
        /// </summary>
        /// <remarks>
        /// Метод позволяет пользователю выбрать поле для поиска (Имя, Фамилия, Телефон) и ввести текст для поиска.
        /// Если список контактов пуст, выводится соответствующее сообщение.
        /// Результаты поиска отображаются в виде таблицы с помощью метода <see cref="ShowContactsInTable"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Выбрасывается, если выбрано неизвестное поле для поиска.</exception>
        public void SearchContacts()
        {
            AnsiConsole.Clear();
            if (_contacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Список контактов пуст. Нечего искать.[/]");
                return;
            }

            var field = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan1]Выберите поле для поиска:[/]")
                    .PageSize(5)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(["Имя", "Фамилия", "Телефон", "Отмена"])
            );

            if (field == "Отмена")
                return;

            string query = AnsiConsole.Ask<string>($"Введите текст для поиска по полю '{field}': ");
            if (string.IsNullOrWhiteSpace(query))
            {
                AnsiConsole.MarkupLine("[red]Пустой запрос. Поиск отменен.[/]");
                return;
            }

            IEnumerable<Contact> results = field switch
            {
                "Имя" => _contacts.Where(c => c.FirstName.Contains(query, StringComparison.OrdinalIgnoreCase)),
                "Фамилия" => _contacts.Where(c => c.SecondName.Contains(query, StringComparison.OrdinalIgnoreCase)),
                "Телефон" => _contacts.Where(c => c.Phone.Contains(query, StringComparison.OrdinalIgnoreCase)),
                _ => throw new InvalidOperationException("Неизвестное поле для поиска.")
            };

            List<Contact> resultList = results.ToList();
            if (resultList.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Контакты не найдены.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[green]Результаты поиска:[/]");
                ShowContactsInTable(resultList);
            }
        }

        /// <summary>
        /// Редактирует выбранный пользователем контакт.
        /// </summary>
        /// <remarks>
        /// Метод позволяет пользователю выбрать контакт по ID и выбрать параметр, который он хочет изменить.
        /// Если список контактов пуст, выводится соответствующее сообщение.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Выбрасывается, если выбрано неизвестное поле для поиска.</exception>
        public void EditContact()
        {
            AnsiConsole.Clear();
            if (_contacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Список контактов пуст. Нечего редактировать.[/]");
                return;
            }

            if (!TryReadInput("Введите ID контакта для редактирования: ", "^\\d+$", out var inputId))
            {
                AnsiConsole.MarkupLine("[red]Операция отменена.[/]");
                return;
            }

            int id = int.Parse(inputId);
            var contactToEdit = _contacts.Find(c => c.Id == id);
            if (contactToEdit == null)
            {
                AnsiConsole.MarkupLine($"[red]Контакт с ID {id} не найден.[/]");
                return;
            }

            while (true)
            {
                var field = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan1]Выберите поле для редактирования:[/]")
                    .PageSize(5)
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(["Имя", "Фамилия", "Телефон", "Email", "Завершить редактирование"])
            );

                if (field == "Завершить редактирование")
                {
                    AnsiConsole.MarkupLine("[green]Редактирование завершено.[/]");
                    return;
                }

                string pattern = field switch
                {
                    "Имя" => "^[A-ZА-Я][a-zа-я]+$",
                    "Фамилия" => "^[A-ZА-Я][a-zа-я]+$",
                    "Телефон" => "^\\+7\\d{10}$",
                    "Email" => @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
                    _ => throw new InvalidOperationException("Неизвестное поле.")
                };

                if (!TryReadInput($"Введите новое значение для поля '{field}': ", pattern, out var newValue))
                {
                    AnsiConsole.MarkupLine("[red]Операция отменена.[/]");
                    return;
                }

                switch (field)
                {
                    case "Имя":
                        contactToEdit.FirstName = newValue;
                        break;
                    case "Фамилия":
                        contactToEdit.SecondName = newValue;
                        break;
                    case "Телефон":
                        contactToEdit.Phone = newValue;
                        break;
                    case "Email":
                        contactToEdit.Email = newValue;
                        break;
                }

                AnsiConsole.MarkupLine($"[green]Поле '{field}' успешно обновлено.[/]");
            }
        }

        /// <summary>
        /// Удаляет выбранный пользователем контакт.
        /// </summary>
        /// <remarks>
        /// Метод позволяет пользователю выбрать контакт по ID, который он хочет изменить.
        /// Если список контактов пуст, выводится соответствующее сообщение.
        /// </remarks>
        public void RemoveContact()
        {
            AnsiConsole.Clear();

            if (_contacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Список контактов пуст. Нечего удалять.[/]");
                return;
            }

            if (!TryReadInput("Введите целочисленный ID контакта для удаления: ", "^\\d+$", out var inputId))
            {
                AnsiConsole.MarkupLine("[red]Операция отменена.[/]");
                return;
            }

            int id = int.Parse(inputId);
            var contact = _contacts.Find(c => c.Id == id);
            if (contact != null)
            {
                _contacts.Remove(contact);
                _usedIds.Remove(id);
                _freeIds.Add(id);
                AnsiConsole.MarkupLine($"[green]Контакт с ID {id} удален.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Контакт с ID {id} не найден.[/]");
            }
        }

        /// <summary>
        /// Читает данные от пользователя в соответствии с регулярным выражением.
        /// </summary>
        /// <param name="prompt">Сообщение пользователю о том, что нужно вводить.</param>
        /// <param name="pattern">Регулярное выражение.</param>
        /// <param name="result">Результат чтения данных.</param>
        /// <returns></returns>
        private bool TryReadInput(string prompt, string pattern, out string? result)
        {
            AnsiConsole.Write(prompt);
            while (true)
            {
                string input = Console.ReadLine()!;
                if (Regex.IsMatch(input.Trim(), pattern))
                {
                    result = input.Trim();
                    return true;
                }

                AnsiConsole.Markup("[red]Некорректный ввод. Попробуйте снова:[/]");
            }
        }

        /// <summary>
        /// Генерирует уникальный ID для каждого нового контакта. Учитывает оСВОбодившиеся id после удаления контактов. 
        /// </summary>
        /// <returns>Целочисленное значение нового ID.</returns>
        private int GenerateUniqueId()
        {
            if (_freeIds.Count > 0)
            {
                int id = _freeIds.Min;
                _freeIds.Remove(id);
                return id;
            }

            if (_usedIds.Count == 0)
                return 0;

            int previous = -1; // Начинаем с -1, чтобы проверить 0.
            foreach (int current in _usedIds)
            {
                if (current - previous > 1)
                    return previous + 1; // Возвращаем первый пропущенный ID.
                previous = current;
            }

            return _usedIds.Max + 1;
        }

        /// <summary>
        /// Добавляет к существующему списку контактов новый. ИСпользует метод TryReadInput, чтобы получить от пользователя
        /// корректные данные.
        /// </summary>
        public void AddContact()
        {
            AnsiConsole.Clear();

            Contact contact = new();

            if (!TryReadInput("Введите имя контакта: ", "^[A-ZА-Я][a-zа-я]+$", out var firstName))
                return;
            contact.FirstName = firstName;

            if (!TryReadInput("Введите фамилию контакта: ", "^[A-ZА-Я][a-zа-я]+$", out var secondName))
                return;
            contact.SecondName = secondName;

            if (!TryReadInput("Введите телефон контакта (формат: +7(10 цифр)): ", "^\\+7\\d{10}$", out var phone))
                return;
            contact.Phone = phone;

            if (!TryReadInput("Введите email контакта (например: user@example.ru): ", @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", out var email))
                return;
            contact.Email = email;

            int uniqueId = GenerateUniqueId();
            contact.Id = uniqueId;
            _usedIds.Add(uniqueId);
            _contacts.Add(contact);

            AnsiConsole.MarkupLine("[green]Контакт успешно добавлен![/]");
        }

        /// <summary>
        /// Запускает по выбору пользователя сортировку/фильтрацию контактов.
        /// </summary>
        public void FilterAndSortOptions()
        {
            while (true)
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan1]Главное фильтрации и сортировки:[/]")
                        .PageSize(10)
                        .HighlightStyle(new Style(Color.Cyan1))
                        .AddChoices(
                        [
                        "Фильтрация",
                        "Сортировка",
                        "Вернуться в меню"
                        ])
                );


                switch (choice)
                {
                    case "Фильтрация":
                        InteractiveFilterContacts();
                        break;
                    case "Сортировка":
                        InteractiveSortContacts();
                        break;
                    case "Вернуться в меню":
                        return;
                }
            }
        }

        /// <summary>
        /// Предоставляет интерактивный интерфейс для фильтрации контактов по выбранному полю и тексту.
        /// </summary>
        /// <remarks>
        /// Метод позволяет пользователю выбрать поле для фильтрации и ввести текст для фильтрации.
        /// Если список контактов пуст, выводится соответствующее сообщение.
        /// Результаты фильтрации отображаются в виде таблицы с помощью метода <see cref="ShowContactsInTable"/>.
        /// Пользователь может повторять фильтрацию по другим полям до тех пор, пока не выберет завершить процесс.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Выбрасывается, если выбрано неизвестное поле для фильтрации.</exception>
        private void InteractiveFilterContacts()
        {
            AnsiConsole.Clear();
            if (_contacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Список контактов пуст. Нечего фильтровать.[/]");
                return;
            }

            while (true)
            {
                // Меню выбора поля для фильтрации.
                var field = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan1]Выберите поле для фильтрации:[/]")
                        .PageSize(5)
                        .HighlightStyle(new Style().Foreground(Color.Cyan1))
                        .AddChoices(["Имя", "Фамилия", "Телефон", "Email", "Дата рождения", "Отмена"])
                );

                if (field == "Отмена")
                {
                    break;
                }

                // Запрос текста для фильтрации.
                AnsiConsole.MarkupLine($"[cyan1]Введите текст для фильтрации по полю '{field}':[/]");
                string filterText = AnsiConsole.Ask<string>("");

                // Фильтрация контактов.
                List<Contact> filteredContacts = field switch
                {
                    "Имя" => _contacts.Where(c => c.FirstName.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList(),
                    "Фамилия" => _contacts.Where(c => c.SecondName.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList(),
                    "Телефон" => _contacts.Where(c => c.Phone.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList(),
                    "Email" => _contacts.Where(c => c.Email.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList(),
                    "Дата рождения" => _contacts.Where(c => c.DateOfBirth.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList(),
                    _ => throw new InvalidOperationException("Неизвестное поле для фильтрации.")
                };

                // Отображение отфильтрованных контактов.
                AnsiConsole.Clear();
                if (filteredContacts.Count == 0)
                {
                    AnsiConsole.MarkupLine("[red]Контакты не найдены.[/]");
                }
                else
                {
                    ShowContactsInTable(filteredContacts);
                }

                // Запрос на повторную фильтрацию.
                var continueFiltering = AnsiConsole.Confirm("Хотите выполнить фильтрацию по другому полю?");
                if (!continueFiltering)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Предоставляет интерактивный интерфейс для сортировки контактов по выбранному полю.
        /// </summary>
        /// <remarks>
        /// Метод позволяет пользователю выбрать поле для сортировки.
        /// Если список контактов пуст, выводится соответствующее сообщение.
        /// Результаты сортировки отображаются в виде таблицы с помощью метода <see cref="ShowContactsInTable"/>.
        /// Пользователь может повторять сортировку по другим полям до тех пор, пока не выберет завершить процесс.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Выбрасывается, если выбрано неизвестное поле для фильтрации.</exception>
        private void InteractiveSortContacts()
        {
            AnsiConsole.Clear();
            if (_contacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Список контактов пуст. Нечего сортировать.[/]");
                return;
            }

            while (true)
            {
                // Меню выбора поля для сортировки.
                var field = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan1]Выберите поле для сортировки:[/]")
                        .PageSize(5)
                        .HighlightStyle(new Style().Foreground(Color.Cyan1))
                        .AddChoices(["Имя", "Фамилия", "Телефон", "Email", "Дата рождения", "Отмена"])
                );

                if (field == "Отмена")
                {
                    break;
                }

                // Сортировка контактов.
                List<Contact> sortedContacts = field switch
                {
                    "Имя" => [.. _contacts.OrderBy(c => c.FirstName)],
                    "Фамилия" => [.. _contacts.OrderBy(c => c.SecondName)],
                    "Телефон" => [.. _contacts.OrderBy(c => c.Phone)],
                    "Email" => [.. _contacts.OrderBy(c => c.Email)],
                    "Дата рождения" => [.. _contacts
                        .OrderBy(c =>
                        {
                            if (c.DateOfBirth == "Неизвестно") return DateTime.MaxValue; // "Неизвестно" в конец.
                            return ParseDateOfBirth(c.DateOfBirth); // Преобразуем dd.mm в DateTime.
                        })],
                    _ => throw new InvalidOperationException("Неизвестное поле для сортировки.")
                };

                // Отображение отсортированных контактов.
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine($"[cyan1]Контакты отсортированы по полю '{field}':[/]");
                ShowContactsInTable(sortedContacts);

                // Запрос на повторную сортировку.
                var continueSorting = AnsiConsole.Confirm("Хотите отсортировать по другому полю?");
                if (!continueSorting)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Преобразует строку с датой рождения в формате "день.месяц" в объект <see cref="DateTime"/>.
        /// </summary>
        /// <param name="dateOfBirth">Строка с датой рождения в формате "день.месяц".</param>
        /// <returns>Объект <see cref="DateTime"/>, представляющий дату рождения.</returns>
        private DateTime ParseDateOfBirth(string dateOfBirth)
        {
            // Разбиваем строку на день и месяц.
            var parts = dateOfBirth.Split('.');
            int day = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);

            // Используем фиксированный год для создания полноценной даты.
            return new DateTime(2025, month, day);
        }

        /// <summary>
        /// Отображает список контактов в виде таблицы с возможностью скроллинга.
        /// </summary>
        /// <param name="list">Список контактов для отображения.</param>
        private void ShowContactsInTable(List<Contact> list)
        {
            AnsiConsole.Clear();
            if (list.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Список контактов пуст.[/]");
                return;
            }

            int currentIndex = 0; // Текущий индекс начала отображаемых данных.
            int pageSize = 10;    // Количество строк на странице.

            while (true)
            {
                AnsiConsole.Clear();

                // Ограничиваем отображаемые данные.
                var page = list.Skip(currentIndex).Take(pageSize).ToList();
                var table = CreateTable(page);

                // Отображаем таблицу в панели.
                AnsiConsole.Write(
                    new Panel(table)
                        .Header($"[cyan1]Контакты[/]")
                        .BorderColor(Color.Cyan1)
                );

                // Подсказка для пользователя.
                AnsiConsole.MarkupLine("[grey](Используйте стрелки ВВЕРХ/ВНИЗ для прокрутки, ESC для выхода)[/]");

                // Обработка нажатия клавиш.
                var keyInfo = Console.ReadKey(intercept: true);
                switch (keyInfo.Key)
                {
                    case ConsoleKey.UpArrow:
                        if (currentIndex > 0)
                            currentIndex--; // Прокрутка вверх.
                        break;

                    case ConsoleKey.DownArrow:
                        if (currentIndex < list.Count - pageSize)
                            currentIndex++; // Прокрутка вниз.
                        break;

                    case ConsoleKey.PageUp:
                        currentIndex = Math.Max(0, currentIndex - pageSize); // Перелистывание вверх.
                        break;

                    case ConsoleKey.PageDown:
                        currentIndex = Math.Min(list.Count - pageSize, currentIndex + pageSize); // Перелистывание вниз.
                        break;

                    case ConsoleKey.Escape:
                        return; // Выход из режима прокрутки.
                }
            }
        }

        /// <summary>
        /// Создает таблицу для отображения списка контактов.
        /// </summary>
        /// <param name="list">Список контактов для отображения.</param>
        /// <returns>Объект таблицы.</returns>
        private Table CreateTable(List<Contact> list)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[cyan1]ID[/]")
                .AddColumn("[cyan1]Имя[/]")
                .AddColumn("[cyan1]Фамилия[/]")
                .AddColumn("[cyan1]Телефон[/]")
                .AddColumn("[cyan1]Email[/]")
                .AddColumn("[cyan1]Дата рождения[/]");

            foreach (var contact in list)
            {
                table.AddRow(
                    contact.Id.ToString(),
                    contact.FirstName,
                    contact.SecondName,
                    contact.Phone,
                    contact.Email,
                    contact.DateOfBirth
                );
            }

            return table;
        }

        /// <summary>
        /// Выводит все контакты в заданном формате варианта 6.
        /// </summary>
        public void ShowContacts()
        {
            if (_contacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Список контактов пуст.[/]");
                return;
            }

            foreach (Contact item in _contacts)
            {
                Console.WriteLine($"ID: {item.Id}");
                Console.WriteLine($"Имя: {item.FirstName}");
                Console.WriteLine($"Фамилия: {item.SecondName}");
                Console.WriteLine($"Телефон: {item.Phone}");
                Console.WriteLine($"Email: {item.Email}");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Отображает список контактов, у которых день рождения на текущей неделе.
        /// </summary>
        /// <remarks>
        /// Метод фильтрует контакты, у которых день рождения приходится на текущую неделю.
        /// Если список контактов пуст, выводится соответствующее сообщение.
        /// Если на текущей неделе нет дней рождения, выводится сообщение об этом.
        /// Результаты отображаются в виде таблицы с помощью метода <see cref="ShowContactsInTable"/>.
        /// </remarks>
        public void ShowUpcomingBirthdays()
        {
            if (_contacts.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Список контактов пуст. Нечего показывать.[/]");
                return;
            }

            // Получаем текущую дату.
            DateTime today = DateTime.Today;

            // Определяем начало и конец текущей недели.
            DateTime startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            DateTime endOfWeek = startOfWeek.AddDays(6);

            // Фильтруем контакты с днями рождения на текущей неделе.
            var upcomingBirthdays = _contacts
                .Where(c => c.DateOfBirth != "Неизвестно") // Исключаем контакты без даты рождения.
                .Select(c =>
                {
                    var dateParts = c.DateOfBirth.Split('.');
                    if (dateParts.Length != 2)
                    {
                        throw new FormatException("Некорректный формат даты рождения.");
                    }

                    int day = int.Parse(dateParts[0]);
                    int month = int.Parse(dateParts[1]);

                    // Создаем дату с текущим годом.
                    DateTime birthdayThisYear = new(today.Year, month, day);

                    // Если день рождения уже прошел в этом году, берем следующий год.
                    if (birthdayThisYear < today)
                    {
                        birthdayThisYear = birthdayThisYear.AddYears(1);
                    }

                    return new { Contact = c, Birthday = birthdayThisYear };

                })
                .Where(x => x != null) // Исключаем контакты с ошибками.
                .Where(x => x.Birthday >= startOfWeek && x.Birthday <= endOfWeek) // Фильтруем по текущей неделе.
                .OrderBy(x => x.Birthday) // Сортируем по ближайшим дням рождения.
                .ToList();

            // Выводим результаты.
            if (upcomingBirthdays.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]На текущей неделе дней рождения нет.[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[cyan1]Ближайшие дни рождения (с {startOfWeek:dd.MM} по {endOfWeek:dd.MM}):[/]");
            var filteredContacts = upcomingBirthdays.Select(x => x.Contact).ToList();
            ShowContactsInTable(filteredContacts);
        }
    }
}
