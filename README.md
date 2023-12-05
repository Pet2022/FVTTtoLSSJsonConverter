# FVTTtoLSSJsonConverter v1.0.0

Данная программа предназначена для конвертации экспортированного из Foundry VTT персонажа в формат [LongStoryShort.app](https://longstoryshort.app).
Есть поддержка массовой конвертации.

## Как использовать
1) Распакуйте проект в любую директорию
2) Положите в папку SourceJSONs экспортированные из фаундри чарники
3) Запустите FVTTtoLSSCharConverter.exe и дождитесь окончания работы.
4) Если всё пройдет успешно, то в папке OutputJSONs появятся JSON с припиской _lss_converted
5) Можно загружать в LongStoryShort и пользоваться


Программа тестировалась на версиях модуля dnd 2.3.1 и 2.4.0, но могли быть учтены не все ньюансы.

Вопросы, баги и предложениям можно обсудить либо на дискорд сервере [longstoryshort.app](https://discord.com/channels/950816091377135666/1171844062219878491) в соответствующей ветке обсуждений, либо в этом репозитории в разделе обсуждений.

##ToDo:
- [ ] Рефакторинг
- [ ] Добавить в формулу урона оружием бонус урона самого оружия, +1, +2, +3
- [ ] Добавить учет бонусов к характеристикам от вещей типа Диадемы интеллекта или Пояса силы великана
- [ ] Вынести локализацию во внешние файлы
- [ ] Подготовить 3 набора файлов локализации, английская, ру хоббиков, ру дндсу
- [ ] Вынести во внешний файлик настройки путей программы инпут, аутпут, темплейтс
- [ ] Предусмотреть чтобы при первом запуске генерились все необходимые папочки рядом с экзешником
- [ ] Обратный частичный залив значений из лсс в фаундри (взяли чарник в фаундри, поиграли в лсс, залили в этот же чарник фаундри изменения из лсс и залили чарник обратно в фаундри)
