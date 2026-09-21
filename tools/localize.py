"""Writes Localization/<lang>/TIProjectTemplate.<lang> for every language the game ships.

Terra Invicta does not fall back to English. LocalizationManager.Find returns the key itself
when it is missing, so a player on another language would see
"TIProjectTemplate.displayName.Project_MareNostrum" in the tech tree. Every language therefore
needs every key.

English is the source. The other languages are translated here and have not been reviewed by
native speakers.
"""

import io
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ORDER = ["Project_CataTweaks_CouncilSize7", "Project_CataTweaks_CouncilSize8",
         "Project_TheSunNeverSets", "Project_MareNostrum", "Project_ImperiumSineFine",
         "Project_NewLiberia"]

# language -> project -> (displayName, summary, description)
T = {}

T["chs"] = {
 "Project_CataTweaks_CouncilSize7": ("深层卧底管理员",
  "第七个席位，经费来自原本用于在他人议会中安插特工的预算。",
  "在对手议会内部经营一名线人从来都不便宜。一名联络官、一名中间人、一条必须在自身成功中幸存的传递路线，"
  "还有一名把整个职业生涯押在一个可能说谎的人身上的专案官。隐秘行动教会我们同时维持两名这样的人员。"
  "本项目让我们看清账本一直在说的事实：同样的机构，转向内部并配上工牌，就能在我们自己的会议桌旁多安排一位自己人。"
  "我们不是在招募间谍，而是把间谍花在自己身上。"),
 "Project_CataTweaks_CouncilSize8": ("影子内阁",
  "第八个席位，最后一名联络官也从寒冷中归来。",
  "每个议会都维持着第二个议会：万一第一个被逮捕、名誉扫地或化为蒸汽，他们就会接手。"
  "我们的第二议会一直在安全屋里开会，费用从情报经费中支出。这是个利落的安排，直到真正需要它、却没人找得到它的那一天。"
  "所以我们让它就座。最后一名联络官交出他的网络，最后一名中间人停止活动，"
  "那把曾资助我们在对手内阁中设立监听站的椅子，被搬上楼来，摆在会议桌的首位。"
  "如今没有什么还能瞒过我们，除非是我们自己选择不再注视的事物。"),
 "Project_TheSunNeverSets": ("日不落",
  "我们重申整个帝国的主张，而不仅仅是移民自治领。",
  "《联邦重建》取回了容易的那一半：那些从未真正离开、旗帜一角仍带着我们纹章的自治领。"
  "地图上的其余部分才始终是重点。德里与达卡、拉各斯与内罗毕、好望角与海峡、糖业诸岛，"
  "以及那十三个溜走的殖民地，全都曾由伦敦一平方英里之内的地方管理。"
  "我们并不是在请求归还，而是在办理文书手续，声明它从未停止属于我们，其余的自会随之而来。"),
 "Project_MareNostrum": ("我们的海",
  "共和国重申其对地中海世界的权威。",
  "无论里程碑被重新漆成什么字样，每条道路仍然通向这座城市。"
  "各行省之间的那片海从来不是边界，它是一座宅邸的中庭，只是住客们变得健忘了。"
  "西班牙、高卢、阿非利加、阿凯亚、亚细亚、埃及、叙利亚，这些名字之所以留在我们的地图上，"
  "只因为从没有人想出更好的名字。我们从海开始，因为海属于我们。"),
 "Project_ImperiumSineFine": ("无疆之帝国",
  "不列颠尼亚、东方诸路、阿克苏姆，以及大洋彼岸。",
  "维吉尔许诺了一个没有尽头的帝国，而测绘员们照字面理解了这句话。"
  "寒冷边缘的不列颠尼亚，军团行军并多半为之后悔的亚美尼亚与美索不达米亚，尼罗河上游的努比亚，"
  "以及红海航线尽头的阿克苏姆，那里的泥土中会翻出我们的钱币，因为我们的商人最先抵达。"
  "然后是大洋，古人以为那是世界的尽头，结果它只是很宽而已。"
  "对岸有陆地，目前由在我们停止眺望十五个世纪之后才到达那里的人管理着。"
  "一个没有尽头的帝国不承认海洋是尽头，它只承认那是一个尚未测绘的行省。"),
 "Project_NewLiberia": ("新利比里亚",
  "蒙罗维亚、弗里敦与利伯维尔：一段海岸，一个共同的创建理念。",
  "三个强权在彼此相隔不到四十年的时间里产生了同样的念头，却没有一个想到要互相通气。"
  "美国人把获释的奴隶送上蒙罗维亚的岸，英国人送到弗里敦，法国人送到利伯维尔，"
  "那名字的意思正是自由之城，以免有人没领会这一点。三者之间是克鲁海岸，"
  "为这三支船队提供水手的正是那里的人。这些定居点的后代彼此之间的共同点，"
  "远多于他们与那些把他们扔在此地的帝国，而他们已经开始这样说了。"),
}

T["cht"] = {
 "Project_CataTweaks_CouncilSize7": ("深層臥底管理員",
  "第七個席位，經費來自原本用於在他人議會中安插特工的預算。",
  "在對手議會內部經營一名線人從來都不便宜。一名聯絡官、一名中間人、一條必須在自身成功中倖存的傳遞路線，"
  "還有一名把整個職業生涯押在一個可能說謊的人身上的專案官。隱密行動教會我們同時維持兩名這樣的人員。"
  "本專案讓我們看清帳本一直在說的事實：同樣的機構，轉向內部並配上工牌，就能在我們自己的會議桌旁多安排一位自己人。"
  "我們不是在招募間諜，而是把間諜花在自己身上。"),
 "Project_CataTweaks_CouncilSize8": ("影子內閣",
  "第八個席位，最後一名聯絡官也從寒冷中歸來。",
  "每個議會都維持著第二個議會：萬一第一個被逮捕、名譽掃地或化為蒸汽，他們就會接手。"
  "我們的第二議會一直在安全屋裡開會，費用從情報經費中支出。這是個俐落的安排，直到真正需要它、卻沒人找得到它的那一天。"
  "所以我們讓它就座。最後一名聯絡官交出他的網絡，最後一名中間人停止活動，"
  "那把曾資助我們在對手內閣中設立監聽站的椅子，被搬上樓來，擺在會議桌的首位。"
  "如今沒有什麼還能瞞過我們，除非是我們自己選擇不再注視的事物。"),
 "Project_TheSunNeverSets": ("日不落",
  "我們重申整個帝國的主張，而不僅僅是移民自治領。",
  "《聯邦重建》取回了容易的那一半：那些從未真正離開、旗幟一角仍帶著我們紋章的自治領。"
  "地圖上的其餘部分才始終是重點。德里與達卡、拉哥斯與奈洛比、好望角與海峽、糖業諸島，"
  "以及那十三個溜走的殖民地，全都曾由倫敦一平方英里之內的地方管理。"
  "我們並不是在請求歸還，而是在辦理文書手續，聲明它從未停止屬於我們，其餘的自會隨之而來。"),
 "Project_MareNostrum": ("我們的海",
  "共和國重申其對地中海世界的權威。",
  "無論里程碑被重新漆成什麼字樣，每條道路仍然通向這座城市。"
  "各行省之間的那片海從來不是邊界，它是一座宅邸的中庭，只是住客們變得健忘了。"
  "西班牙、高盧、阿非利加、阿凱亞、亞細亞、埃及、敘利亞，這些名字之所以留在我們的地圖上，"
  "只因為從沒有人想出更好的名字。我們從海開始，因為海屬於我們。"),
 "Project_ImperiumSineFine": ("無疆之帝國",
  "不列顛尼亞、東方諸路、阿克蘇姆，以及大洋彼岸。",
  "維吉爾許諾了一個沒有盡頭的帝國，而測繪員們照字面理解了這句話。"
  "寒冷邊緣的不列顛尼亞，軍團行軍並多半為之後悔的亞美尼亞與美索不達米亞，尼羅河上游的努比亞，"
  "以及紅海航線盡頭的阿克蘇姆，那裡的泥土中會翻出我們的錢幣，因為我們的商人最先抵達。"
  "然後是大洋，古人以為那是世界的盡頭，結果它只是很寬而已。"
  "對岸有陸地，目前由在我們停止眺望十五個世紀之後才到達那裡的人管理著。"
  "一個沒有盡頭的帝國不承認海洋是盡頭，它只承認那是一個尚未測繪的行省。"),
 "Project_NewLiberia": ("新賴比瑞亞",
  "蒙羅維亞、自由城與自由市：一段海岸，一個共同的創建理念。",
  "三個強權在彼此相隔不到四十年的時間裡產生了同樣的念頭，卻沒有一個想到要互相通氣。"
  "美國人把獲釋的奴隸送上蒙羅維亞的岸，英國人送到自由城，法國人送到自由市，"
  "那名字的意思正是自由之城，以免有人沒領會這一點。三者之間是克魯海岸，"
  "為這三支船隊提供水手的正是那裡的人。這些定居點的後代彼此之間的共同點，"
  "遠多於他們與那些把他們扔在此地的帝國，而他們已經開始這樣說了。"),
}

T["cze"] = {
 "Project_CataTweaks_CouncilSize7": ("Řídící důstojníci hlubokého krytí",
  "Sedmé křeslo, zaplacené z rozpočtu, který dříve živil agenta v cizí radě.",
  "Vést zdroj uvnitř soupeřovy rady nikdy nebylo levné. Řídící důstojník, prostředník, "
  "kurýrní trasa, která musí přežít vlastní úspěch, a operativec, jehož celý pracovní život "
  "stojí na jednom člověku, který možná lže. Skryté operace nás naučily udržet dva takové "
  "najednou. Tento projekt nás učí to, co účetní kniha říkala celou dobu: tentýž aparát, "
  "obrácený dovnitř a opatřený jmenovkou, posadí dalšího našeho člověka k našemu vlastnímu "
  "stolu. Nenajímáme špeha. Utrácíme špeha za sebe samé."),
 "Project_CataTweaks_CouncilSize8": ("Stínový kabinet",
  "Osmé křeslo, a poslední řídící důstojník se vrací z chladu.",
  "Každá rada si drží druhou radu: lidi, kteří by převzali moc, kdyby první byla zatčena, "
  "zdiskreditována nebo vypařena. Ta naše se scházela v konspiračních bytech a platila se z "
  "rozpočtu na zpravodajství. Úhledné uspořádání až do dne, kdy je jí potřeba a nikdo ji "
  "nedokáže najít. Tak ji usadíme. Poslední řídící důstojník odevzdá svou síť, poslední "
  "prostředník je stažen a křeslo, které financovalo odposlech v soupeřově kabinetu, se "
  "vynese nahoru a postaví do čela stolu. Nic už před námi není skryto, kromě toho, co jsme "
  "se sami rozhodli přestat sledovat."),
 "Project_TheSunNeverSets": ("Slunce nikdy nezapadá",
  "Znovu uplatňujeme nároky celého impéria, nejen osadnických dominií.",
  "Obnovené společenství vrátilo tu snadnou polovinu: dominia, která nikdy doopravdy neodešla "
  "a jejichž vlajky dodnes nesou v rohu tu naši. Zbytek mapy byl vždycky tím hlavním. Dillí a "
  "Dháka, Lagos a Nairobi, Kapsko a Úžiny, cukrové ostrovy a těch třináct kolonií, které nám "
  "utekly, to vše kdysi spravovala jediná čtvereční míle Londýna. Nežádáme o to zpět. Jen "
  "podáváme papír, na kterém stojí, že to nikdy nepřestalo být naše, a necháme zbytek "
  "následovat."),
 "Project_MareNostrum": ("Mare Nostrum",
  "Republika znovu uplatňuje svou moc nad středomořským světem.",
  "Každá cesta stále vede do tohoto města, ať už jsou milníky přemalované na cokoli. Moře mezi "
  "provinciemi není hranicí a nikdy nebylo. Je to nádvoří jediného domu, jehož nájemníci "
  "zapomněli. Hispánie, Galie, Afrika, Achaia, Asie, Egypt, Sýrie. Ta jména přežívají na našich "
  "mapách proto, že nikdo nikdy nevymyslel lepší. Začínáme mořem, protože moře je naše."),
 "Project_ImperiumSineFine": ("Imperium Sine Fine",
  "Britannie, východní cesty, Aksum a vzdálený břeh Oceánu.",
  "Vergilius slíbil říši bez konce a zeměměřiči ho vzali doslova. Britannie na chladném okraji, "
  "Arménie a Mezopotámie, kam legie pochodovaly a většinou toho litovaly, Núbie proti proudu "
  "řeky a Aksum na konci trasy Rudým mořem, kde se naše mince nacházejí v hlíně, protože naši "
  "obchodníci dorazili první. A pak Oceán, který dávní považovali za okraj světa a který se "
  "ukazuje být pouze široký. Na druhé straně je země, kterou dnes spravují lidé, jež tam "
  "dorazili patnáct století poté, co jsme se přestali dívat. Říše bez konce neuznává oceán jako "
  "konec. Uznává ho jako nezměřenou provincii."),
 "Project_NewLiberia": ("Nová Libérie",
  "Monrovia, Freetown a Libreville: jedno pobřeží, jedna zakládající myšlenka.",
  "Tři mocnosti dostaly tentýž nápad během čtyřiceti let a ani jednu nenapadlo se domluvit. "
  "Američané vysadili osvobozené otroky v Monrovii, Britové ve Freetownu, Francouzi v "
  "Libreville, tedy ve svobodném městě, kdyby to snad někomu uniklo. Mezi nimi leží pobřeží "
  "Kru, jehož námořníci sloužili na lodích všech tří. Potomci těchto osad mají spolu navzájem "
  "více společného než s impérii, která je sem vysadila, a začali to říkat nahlas."),
}

T["deu"] = {
 "Project_CataTweaks_CouncilSize7": ("Führungsoffiziere der Tiefe",
  "Ein siebter Sitz, bezahlt aus dem Etat, der einen Agenten im Rat eines anderen finanzierte.",
  "Eine Quelle im Rat eines Rivalen zu führen war nie billig. Ein Führungsoffizier, ein "
  "Mittelsmann, eine Kurierroute, die ihren eigenen Erfolg überstehen muss, und ein "
  "Verbindungsoffizier, dessen gesamtes Berufsleben an einem Menschen hängt, der vielleicht "
  "lügt. Verdeckte Operationen brachten uns bei, zwei davon gleichzeitig zu tragen. Dieses "
  "Projekt bringt uns bei, was die Bücher die ganze Zeit sagten: derselbe Apparat, nach innen "
  "gedreht und mit einem Namensschild versehen, setzt einen weiteren der Unseren an unseren "
  "eigenen Tisch. Wir rekrutieren keinen Spion. Wir geben den Spion für uns selbst aus."),
 "Project_CataTweaks_CouncilSize8": ("Schattenkabinett",
  "Ein achter Sitz, und der letzte Führungsoffizier kommt aus der Kälte zurück.",
  "Jeder Rat hält sich einen zweiten Rat: die Leute, die übernehmen würden, wäre der erste "
  "verhaftet, diskreditiert oder verdampft. Unserer tagte in konspirativen Wohnungen und "
  "bezahlte sich aus dem Nachrichtenetat. Eine saubere Lösung, bis zu dem Tag, an dem man ihn "
  "braucht und niemand ihn findet. Also setzen wir ihn an den Tisch. Der letzte "
  "Führungsoffizier gibt sein Netz ab, der letzte Mittelsmann wird abgezogen, und der Stuhl, "
  "der einen Horchposten im Kabinett eines Rivalen finanziert hat, wird nach oben getragen und "
  "an das Kopfende gestellt. Nichts ist uns mehr verborgen außer dem, was wir selbst nicht "
  "mehr beobachten wollten."),
 "Project_TheSunNeverSets": ("Die Sonne geht nie unter",
  "Wir erheben die Ansprüche des ganzen Empire, nicht nur der Siedlerdominien.",
  "Commonwealth Restored holte die leichte Hälfte zurück: die Dominien, die nie wirklich "
  "gegangen sind und deren Flaggen unsere noch in der Ecke tragen. Der Rest der Karte war "
  "immer der Punkt. Delhi und Dhaka, Lagos und Nairobi, das Kap und die Straits, die "
  "Zuckerinseln und die dreizehn Kolonien, die uns entkommen sind, alles einst verwaltet von "
  "einer einzigen Quadratmeile in London. Wir bitten nicht darum, es zurückzubekommen. Wir "
  "reichen den Vorgang ein, der festhält, dass es nie aufgehört hat, unser zu sein, und lassen "
  "den Rest folgen."),
 "Project_MareNostrum": ("Mare Nostrum",
  "Die Republik erhebt erneut Anspruch auf die Welt des Mittelmeers.",
  "Jede Straße führt noch immer in diese Stadt, ganz gleich, worauf man die Meilensteine "
  "umgemalt hat. Das Meer zwischen den Provinzen ist keine Grenze und war es nie. Es ist der "
  "Innenhof eines einzigen Hauses, dessen Bewohner vergesslich geworden sind. Hispanien, "
  "Gallien, Africa, Achaia, Asia, Aegyptus, Syrien. Die Namen überleben auf unseren Karten, "
  "weil nie jemand bessere erfunden hat. Wir beginnen mit dem Meer, denn das Meer ist unser."),
 "Project_ImperiumSineFine": ("Imperium Sine Fine",
  "Britannien, die östlichen Straßen, Aksum und das ferne Ufer des Ozeans.",
  "Vergil versprach ein Reich ohne Ende, und die Landvermesser nahmen ihn beim Wort. Britannien "
  "am kalten Rand, Armenien und Mesopotamien, wohin die Legionen marschierten und es meist "
  "bereuten, Nubien flussaufwärts und Aksum am Ende der Route durch das Rote Meer, wo unsere "
  "Münzen im Boden auftauchen, weil unsere Händler zuerst dort waren. Und dann der Ozean, den "
  "die Alten für das Ende der Welt hielten und der sich als bloß breit erweist. Auf der anderen "
  "Seite liegt Land, das heute von Leuten verwaltet wird, die fünfzehn Jahrhunderte nach uns "
  "dort ankamen. Ein Reich ohne Ende erkennt einen Ozean nicht als Ende an. Es erkennt ihn als "
  "unvermessene Provinz."),
 "Project_NewLiberia": ("Neu-Liberia",
  "Monrovia, Freetown und Libreville: eine Küste, ein Gründungsgedanke.",
  "Drei Mächte hatten binnen vierzig Jahren denselben Einfall, und keine kam auf die Idee, sich "
  "abzustimmen. Die Amerikaner setzten befreite Sklaven in Monrovia an Land, die Briten in "
  "Freetown, die Franzosen in Libreville, der freien Stadt, falls es jemand überhört hätte. "
  "Dazwischen liegt die Kru-Küste, deren Seeleute die Schiffe aller drei bemannten. Die "
  "Nachfahren dieser Siedlungen haben mehr miteinander gemein als mit den Imperien, die sie "
  "hier absetzten, und sie haben begonnen, das auch zu sagen."),
}

T["esp"] = {
 "Project_CataTweaks_CouncilSize7": ("Oficiales de cobertura profunda",
  "Un séptimo asiento, pagado con el presupuesto que sostenía a un agente en el consejo ajeno.",
  "Dirigir a una fuente dentro del consejo de un rival nunca fue barato. Un oficial de enlace, "
  "un intermediario, una ruta de correo que debe sobrevivir a su propio éxito y un oficial de "
  "caso cuya vida laboral entera depende de una persona que quizá esté mintiendo. Operaciones "
  "Encubiertas nos enseñó a sostener a dos a la vez. Este proyecto nos enseña lo que el libro "
  "de cuentas venía diciendo desde siempre: el mismo aparato, vuelto hacia dentro y con una "
  "credencial, sienta a otro de los nuestros en nuestra propia mesa. No estamos reclutando a "
  "un espía. Estamos gastando al espía en nosotros mismos."),
 "Project_CataTweaks_CouncilSize8": ("Gabinete en la sombra",
  "Un octavo asiento, y el último oficial de enlace vuelve del frío.",
  "Todo consejo mantiene un segundo consejo: la gente que tomaría el relevo si el primero fuera "
  "detenido, desacreditado o vaporizado. El nuestro se reunía en pisos francos y se pagaba con "
  "la partida de inteligencia. Un arreglo pulcro hasta el día en que hace falta y nadie logra "
  "encontrarlo. Así que lo sentamos. El último oficial entrega su red, el último intermediario "
  "queda desactivado, y la silla que financiaba un puesto de escucha en el gabinete de un rival "
  "sube por la escalera y se coloca a la cabecera de la mesa. Ya nada se nos oculta, salvo lo "
  "que decidimos dejar de mirar."),
 "Project_TheSunNeverSets": ("El sol nunca se pone",
  "Reafirmamos las reclamaciones de todo el Imperio, no solo de los dominios de colonos.",
  "Commonwealth Restaurada recuperó la mitad fácil: los dominios que nunca se marcharon del "
  "todo, cuyas banderas siguen llevando la nuestra en una esquina. El resto del mapa siempre "
  "fue lo importante. Delhi y Daca, Lagos y Nairobi, el Cabo y los Estrechos, las islas del "
  "azúcar, y las trece colonias que se nos escaparon, todo ello administrado en su día desde "
  "una sola milla cuadrada de Londres. No estamos pidiendo que nos lo devuelvan. Estamos "
  "presentando el papeleo que dice que nunca dejó de ser nuestro, y dejando que lo demás siga."),
 "Project_MareNostrum": ("Mare Nostrum",
  "La República reafirma su autoridad sobre el mundo mediterráneo.",
  "Todos los caminos siguen llevando a esta ciudad, por mucho que hayan repintado los mojones. "
  "El mar entre las provincias no es una frontera y nunca lo fue. Es el patio de una sola casa "
  "cuyos inquilinos se han vuelto olvidadizos. Hispania, Galia, África, Acaya, Asia, Egipto, "
  "Siria. Los nombres sobreviven en nuestros mapas porque nadie ha inventado nunca otros "
  "mejores. Empezamos por el mar, porque el mar es nuestro."),
 "Project_ImperiumSineFine": ("Imperium Sine Fine",
  "Britania, las rutas de oriente, Aksum, y la orilla lejana del Océano.",
  "Virgilio prometió un imperio sin fin, y los agrimensores lo tomaron al pie de la letra. "
  "Britania en el borde frío, Armenia y Mesopotamia, adonde marcharon las legiones y casi "
  "siempre lo lamentaron, Nubia río arriba, y Aksum al final de la ruta del mar Rojo, donde "
  "nuestras monedas aparecen en la tierra porque nuestros mercaderes llegaron primero. Y luego "
  "el Océano, que los antiguos tomaron por el borde del mundo y que resulta ser meramente "
  "ancho. Al otro lado hay tierra, administrada hoy por gente que llegó quince siglos después "
  "de que dejáramos de mirar. Un imperio sin fin no reconoce un océano como un fin. Lo reconoce "
  "como una provincia sin medir."),
 "Project_NewLiberia": ("Nueva Liberia",
  "Monrovia, Freetown y Libreville: una costa, una misma idea fundacional.",
  "Tres potencias tuvieron la misma ocurrencia en menos de cuarenta años, y a ninguna se le "
  "ocurrió consultarlo con las demás. Los estadounidenses desembarcaron esclavos liberados en "
  "Monrovia, los británicos en Freetown, los franceses en Libreville, la ciudad libre, por si "
  "no quedaba claro. Entre ellas se extiende la costa kru, cuyos marineros tripularon los "
  "barcos de las tres. Los descendientes de aquellos asentamientos tienen más en común entre sí "
  "que con los imperios que los dejaron aquí, y han empezado a decirlo."),
}

T["fr"] = {
 "Project_CataTweaks_CouncilSize7": ("Officiers traitants clandestins",
  "Un septième siège, payé sur le budget qui entretenait un agent dans le conseil d'autrui.",
  "Entretenir une source au sein du conseil d'un rival n'a jamais été bon marché. Un officier "
  "traitant, un intermédiaire, une filière de courrier qui doit survivre à son propre succès, "
  "et un officier de cas dont toute la vie professionnelle repose sur une personne qui ment "
  "peut-être. Les Opérations clandestines nous ont appris à en tenir deux à la fois. Ce projet "
  "nous apprend ce que le registre disait depuis toujours : le même appareil, retourné vers "
  "l'intérieur et muni d'un badge, installe un des nôtres de plus à notre propre table. Nous ne "
  "recrutons pas un espion. Nous dépensons l'espion sur nous-mêmes."),
 "Project_CataTweaks_CouncilSize8": ("Cabinet fantôme",
  "Un huitième siège, et le dernier officier traitant rentre du froid.",
  "Tout conseil entretient un second conseil : ceux qui prendraient la suite si le premier était "
  "arrêté, discrédité ou vaporisé. Le nôtre se réunissait dans des planques et se payait sur la "
  "ligne du renseignement. Arrangement commode, jusqu'au jour où on en a besoin et où personne "
  "ne le retrouve. Nous l'installons donc. Le dernier officier rend son réseau, le dernier "
  "intermédiaire est désactivé, et la chaise qui finançait un poste d'écoute dans le cabinet "
  "d'un rival est montée à l'étage et placée en bout de table. Plus rien ne nous est caché, "
  "sinon ce que nous avons choisi de cesser de surveiller."),
 "Project_TheSunNeverSets": ("Le soleil ne se couche jamais",
  "Nous réaffirmons les revendications de tout l'Empire, et pas seulement des dominions de "
  "peuplement.",
  "Le Commonwealth restauré a repris la moitié facile : les dominions qui ne sont jamais "
  "vraiment partis, dont les drapeaux portent encore le nôtre dans un coin. Le reste de la "
  "carte a toujours été l'essentiel. Delhi et Dacca, Lagos et Nairobi, le Cap et les Détroits, "
  "les îles à sucre, et les treize colonies qui nous ont échappé, tout cela administré jadis "
  "depuis un seul mille carré de Londres. Nous ne demandons pas qu'on nous le rende. Nous "
  "déposons le dossier qui établit que cela n'a jamais cessé d'être à nous, et nous laissons "
  "le reste suivre."),
 "Project_MareNostrum": ("Mare Nostrum",
  "La République réaffirme son autorité sur le monde méditerranéen.",
  "Toutes les routes mènent encore à cette ville, quoi qu'on ait repeint sur les bornes. La mer "
  "entre les provinces n'est pas une frontière et ne l'a jamais été. C'est la cour d'une seule "
  "maison dont les occupants sont devenus oublieux. Hispanie, Gaule, Afrique, Achaïe, Asie, "
  "Égypte, Syrie. Ces noms survivent sur nos cartes parce que personne n'en a jamais inventé de "
  "meilleurs. Nous commençons par la mer, car la mer est à nous."),
 "Project_ImperiumSineFine": ("Imperium Sine Fine",
  "La Bretagne, les routes d'orient, Aksoum, et la rive lointaine de l'Océan.",
  "Virgile a promis un empire sans fin, et les arpenteurs l'ont pris au mot. La Bretagne au bord "
  "froid, l'Arménie et la Mésopotamie où les légions ont marché et l'ont surtout regretté, la "
  "Nubie en amont, et Aksoum au bout de la route de la mer Rouge, où nos pièces ressortent de "
  "la terre parce que nos marchands sont arrivés les premiers. Et puis l'Océan, que les anciens "
  "prenaient pour le bord du monde et qui se révèle seulement large. De l'autre côté il y a une "
  "terre, administrée aujourd'hui par des gens arrivés quinze siècles après que nous avons "
  "cessé de regarder. Un empire sans fin ne reconnaît pas un océan comme une fin. Il y "
  "reconnaît une province non arpentée."),
 "Project_NewLiberia": ("Nouveau Liberia",
  "Monrovia, Freetown et Libreville : une côte, une même idée fondatrice.",
  "Trois puissances ont eu la même idée en moins de quarante ans, et aucune n'a songé à se "
  "concerter. Les Américains ont débarqué des esclaves affranchis à Monrovia, les Britanniques "
  "à Freetown, les Français à Libreville, la ville libre, au cas où le message aurait échappé à "
  "quelqu'un. Entre elles s'étend la côte kru, dont les marins ont armé les navires des trois. "
  "Les descendants de ces établissements ont plus en commun entre eux qu'avec les empires qui "
  "les ont déposés ici, et ils ont commencé à le dire."),
}


T["ita"] = {
 "Project_CataTweaks_CouncilSize7": ("Gestori sotto copertura",
  "Un settimo seggio, pagato con il bilancio che manteneva un agente nel consiglio altrui.",
  "Gestire una fonte dentro il consiglio di un rivale non e mai stato economico. Un gestore, un "
  "intermediario, una linea di corriere che deve sopravvivere al proprio successo, e un ufficiale "
  "di caso la cui intera vita lavorativa dipende da una persona che forse mente. Operazioni "
  "Coperte ci ha insegnato a mantenerne due insieme. Questo progetto ci insegna cio che il "
  "registro diceva da sempre: lo stesso apparato, rivolto verso l'interno e munito di tesserino, "
  "siede un altro dei nostri al nostro stesso tavolo. Non stiamo reclutando una spia. Stiamo "
  "spendendo la spia su noi stessi."),
 "Project_CataTweaks_CouncilSize8": ("Governo ombra",
  "Un ottavo seggio, e l'ultimo gestore rientra dal freddo.",
  "Ogni consiglio ne tiene un secondo: le persone che subentrerebbero se il primo fosse "
  "arrestato, screditato o vaporizzato. Il nostro si riuniva in case sicure e si pagava con la "
  "voce dell'intelligence. Una sistemazione ordinata, fino al giorno in cui serve e nessuno "
  "riesce a trovarla. Percio la facciamo sedere. L'ultimo gestore consegna la sua rete, l'ultimo "
  "intermediario viene ritirato, e la sedia che finanziava una stazione di ascolto nel governo di "
  "un rivale viene portata di sopra e messa a capotavola. Nulla ci e piu nascosto, tranne cio che "
  "abbiamo scelto di smettere di osservare."),
 "Project_TheSunNeverSets": ("Il sole non tramonta mai",
  "Rivendichiamo le pretese dell'intero Impero, non solo dei dominii di coloni.",
  "Commonwealth Restaurato ha ripreso la meta facile: i dominii che non se ne sono mai andati "
  "davvero, le cui bandiere portano ancora la nostra in un angolo. Il resto della mappa e sempre "
  "stato il punto. Delhi e Dacca, Lagos e Nairobi, il Capo e gli Stretti, le isole dello "
  "zucchero, e le tredici colonie che ci sono sfuggite, tutto amministrato un tempo da un solo "
  "miglio quadrato di Londra. Non stiamo chiedendo indietro nulla. Stiamo depositando le carte "
  "che dicono che non ha mai smesso di essere nostro, e lasciamo che il resto segua."),
 "Project_MareNostrum": ("Mare Nostrum",
  "La Repubblica riafferma la propria autorita sul mondo mediterraneo.",
  "Ogni strada porta ancora a questa citta, qualunque cosa abbiano riverniciato sulle pietre "
  "miliari. Il mare tra le province non e un confine e non lo e mai stato. E il cortile di una "
  "sola casa i cui inquilini sono diventati smemorati. Hispania, Gallia, Africa, Acaia, Asia, "
  "Aegyptus, Siria. I nomi sopravvivono sulle nostre mappe perche nessuno ne ha mai inventati di "
  "migliori. Cominciamo dal mare, perche il mare e nostro."),
 "Project_ImperiumSineFine": ("Imperium Sine Fine",
  "Britannia, le vie d'oriente, Aksum, e la riva lontana dell'Oceano.",
  "Virgilio promise un impero senza fine, e gli agrimensori lo presero alla lettera. La Britannia "
  "al margine freddo, l'Armenia e la Mesopotamia dove le legioni marciarono e per lo piu se ne "
  "pentirono, la Nubia risalendo il fiume, e Aksum in fondo alla rotta del Mar Rosso, dove le "
  "nostre monete riaffiorano dalla terra perche i nostri mercanti arrivarono per primi. E poi "
  "l'Oceano, che gli antichi presero per il bordo del mondo e che si rivela soltanto largo. "
  "Dall'altra parte c'e una terra, amministrata oggi da gente giunta quindici secoli dopo che "
  "avevamo smesso di guardare. Un impero senza fine non riconosce un oceano come una fine. Lo "
  "riconosce come una provincia non ancora misurata."),
 "Project_NewLiberia": ("Nuova Liberia",
  "Monrovia, Freetown e Libreville: una costa, una sola idea fondatrice.",
  "Tre potenze ebbero la stessa idea nel giro di quarant'anni, e a nessuna venne in mente di "
  "consultarsi. Gli americani sbarcarono schiavi liberati a Monrovia, i britannici a Freetown, i "
  "francesi a Libreville, la citta libera, semmai qualcuno non avesse colto il punto. In mezzo si "
  "stende la costa Kru, i cui marinai equipaggiarono le navi di tutte e tre. I discendenti di "
  "quegli insediamenti hanno piu in comune tra loro che con gli imperi che li hanno lasciati qui, "
  "e hanno cominciato a dirlo."),
}

T["jpn"] = {
 "Project_CataTweaks_CouncilSize7": ("深層潜入管理官",
  "七つ目の席。他者の評議会に工作員を置くための予算から支払われる。",
  "競合する評議会の内部に情報源を維持することは、決して安価ではなかった。管理官、仲介者、"
  "自らの成功を生き延びねばならない連絡経路、そして嘘をついているかもしれない一人の人間に"
  "職業人生のすべてを賭ける担当官。隠密作戦は、それを同時に二人維持する術を我々に教えた。"
  "本計画が教えるのは、帳簿がずっと語っていたことだ。同じ機構を内側へ向け、身分証を与えれば、"
  "我々自身の卓にもう一人の同胞を座らせられる。我々はスパイを雇っているのではない。"
  "スパイを自分たちのために使っているのだ。"),
 "Project_CataTweaks_CouncilSize8": ("影の内閣",
  "八つ目の席。そして最後の管理官が寒さの中から帰還する。",
  "どの評議会も第二の評議会を抱えている。第一の評議会が逮捕され、信用を失い、あるいは蒸発した"
  "ときに引き継ぐ者たちだ。我々のそれは隠れ家で会合を重ね、情報活動費から支払われてきた。"
  "必要になる日が来て誰も見つけられなくなるまでは、整然とした取り決めだった。"
  "だから座らせる。最後の管理官は自らの網を引き渡し、最後の仲介者は活動を停止し、"
  "競合国の内閣に聴取拠点を置く資金となっていた椅子は、階上へ運ばれ卓の上座に据えられる。"
  "もはや我々に隠されているものはない。我々自身が見るのをやめると決めたもの以外は。"),
 "Project_TheSunNeverSets": ("日の沈まぬ国",
  "入植自治領のみならず、帝国全体の請求権を改めて主張する。",
  "コモンウェルス再建が取り戻したのは容易な半分だった。真に去ったことなどなく、"
  "旗の隅に今も我らの意匠を留める自治領である。地図の残りこそが常に要点だった。"
  "デリーとダッカ、ラゴスとナイロビ、喜望峰と海峡、砂糖諸島、そして逃げ去った十三植民地。"
  "そのすべてがかつてロンドンのわずか一平方マイルから統治されていた。返せと求めているのではない。"
  "それが我らのものであることをやめた事実はないと記す書類を提出し、あとは続かせるだけだ。"),
 "Project_MareNostrum": ("我らが海",
  "共和国は地中海世界に対する権威を改めて主張する。",
  "里程標に何を塗り直そうと、すべての道は今なおこの都市へ通じている。"
  "属州の間に横たわる海は国境ではないし、かつてもそうではなかった。"
  "それは一軒の家の中庭であり、住人が物忘れをするようになっただけのことだ。"
  "ヒスパニア、ガリア、アフリカ、アカイア、アシア、アエギュプトゥス、シリア。"
  "これらの名が我々の地図に残っているのは、誰もより良い名を考案しなかったからにすぎない。"
  "我々は海から始める。海は我々のものだからだ。"),
 "Project_ImperiumSineFine": ("果てなき帝国",
  "ブリタンニア、東方の街道、アクスム、そして大洋の彼方の岸。",
  "ウェルギリウスは果てなき帝国を約束し、測量士たちはそれを文字どおりに受け取った。"
  "寒冷な縁のブリタンニア、軍団が行軍しおおむね後悔したアルメニアとメソポタミア、"
  "川を遡ったヌビア、そして紅海航路の果てのアクスム。そこでは我らの貨幣が土から出てくる。"
  "我らの商人が最初に着いたからだ。そして大洋。古人は世界の縁と見なしたが、"
  "実際には単に広いだけであった。向こう側には陸地があり、我々が見るのをやめて十五世紀の後に"
  "到達した者たちが現在それを統治している。果てなき帝国は大洋を果てとは認めない。"
  "未測量の属州として認めるのみである。"),
 "Project_NewLiberia": ("新リベリア",
  "モンロビア、フリータウン、リーブルヴィル。ひとつの海岸、ひとつの創設理念。",
  "三つの大国が四十年と経たぬうちに同じ着想を得たが、どこも互いに相談しようとは考えなかった。"
  "アメリカ人は解放奴隷をモンロビアに、イギリス人はフリータウンに、フランス人はリーブルヴィル、"
  "すなわち自由の町に上陸させた。要点を取り逃す者がいないようにである。"
  "その間に横たわるのがクルー海岸で、そこの船乗りが三者すべての船に乗り組んだ。"
  "これらの入植地の子孫は、自分たちをここに置いた帝国よりも互いに多くを共有しており、"
  "そう口にし始めている。"),
}

T["kor"] = {
 "Project_CataTweaks_CouncilSize7": ("심층 잠입 관리관",
  "일곱 번째 자리. 남의 평의회에 요원을 두던 예산으로 마련한다.",
  "경쟁 평의회 내부에 정보원을 운용하는 일은 결코 값싸지 않았다. 관리관, 중개인, 스스로의 성공을 "
  "견뎌내야 하는 연락선, 그리고 거짓말을 하고 있을지도 모르는 한 사람에게 직업 인생 전체를 거는 "
  "담당관. 비밀 작전은 그런 인원을 동시에 둘 유지하는 법을 가르쳤다. 이 계획은 장부가 줄곧 말해온 "
  "바를 가르친다. 같은 기구를 안으로 돌리고 명찰을 달아주면, 우리 자신의 탁자에 우리 사람 하나를 "
  "더 앉힐 수 있다는 것이다. 우리는 첩자를 모집하는 것이 아니다. 첩자를 우리 자신에게 쓰는 것이다."),
 "Project_CataTweaks_CouncilSize8": ("그림자 내각",
  "여덟 번째 자리. 그리고 마지막 관리관이 추위에서 돌아온다.",
  "모든 평의회는 두 번째 평의회를 둔다. 첫 번째가 체포되거나 신용을 잃거나 증발했을 때 이어받을 "
  "사람들이다. 우리의 그것은 안가에서 모였고 정보 예산으로 유지되었다. 정작 필요해진 날 아무도 "
  "찾지 못하기 전까지는 깔끔한 방식이었다. 그래서 자리에 앉힌다. 마지막 관리관은 자신의 조직망을 "
  "넘기고, 마지막 중개인은 활동을 멈추며, 경쟁자의 내각에 청취소를 두는 비용을 대던 그 의자는 "
  "위층으로 옮겨져 상석에 놓인다. 이제 우리에게 감춰진 것은 없다. 우리 스스로 보기를 그만둔 것을 "
  "제외하면."),
 "Project_TheSunNeverSets": ("해가 지지 않는다",
  "정착 자치령뿐 아니라 제국 전체의 권리를 다시 주장한다.",
  "코먼웰스 재건은 쉬운 절반을 되찾았다. 진정으로 떠난 적이 없고 깃발 한 귀퉁이에 여전히 우리 "
  "것을 달고 있는 자치령들이다. 지도의 나머지야말로 언제나 요점이었다. 델리와 다카, 라고스와 "
  "나이로비, 희망봉과 해협, 설탕 제도, 그리고 빠져나간 열세 식민지. 그 모두가 한때 런던의 단 "
  "1제곱마일에서 관리되었다. 돌려달라고 청하는 것이 아니다. 그것이 우리 것이기를 멈춘 적이 없다고 "
  "적힌 서류를 제출하고, 나머지는 따라오게 두는 것이다."),
 "Project_MareNostrum": ("우리의 바다",
  "공화국이 지중해 세계에 대한 권위를 다시 주장한다.",
  "이정표에 무엇을 덧칠했든 모든 길은 여전히 이 도시로 이어진다. 속주들 사이의 바다는 국경이 "
  "아니며 한 번도 아니었다. 그것은 한 집의 안뜰이고, 다만 거주자들이 잘 잊게 되었을 뿐이다. "
  "히스파니아, 갈리아, 아프리카, 아카이아, 아시아, 아이귑투스, 시리아. 그 이름들이 우리 지도에 "
  "남아 있는 것은 아무도 더 나은 이름을 고안하지 않았기 때문이다. 우리는 바다에서 시작한다. "
  "바다가 우리의 것이기 때문이다."),
 "Project_ImperiumSineFine": ("끝없는 제국",
  "브리타니아, 동방의 길, 악숨, 그리고 대양 건너의 기슭.",
  "베르길리우스는 끝없는 제국을 약속했고, 측량사들은 그것을 문자 그대로 받아들였다. 차가운 "
  "가장자리의 브리타니아, 군단이 행군했다가 대개 후회한 아르메니아와 메소포타미아, 강을 거슬러 "
  "오른 누비아, 그리고 홍해 항로 끝의 악숨. 그곳 흙에서는 우리 주화가 나온다. 우리 상인이 먼저 "
  "닿았기 때문이다. 그리고 대양. 옛사람들은 세계의 끝이라 여겼으나 실은 그저 넓을 뿐이었다. "
  "건너편에는 땅이 있고, 우리가 바라보기를 멈춘 지 열다섯 세기 뒤에 도착한 이들이 지금 그곳을 "
  "다스린다. 끝없는 제국은 대양을 끝으로 인정하지 않는다. 아직 측량되지 않은 속주로 인정할 뿐이다."),
 "Project_NewLiberia": ("신 라이베리아",
  "몬로비아, 프리타운, 리브르빌. 하나의 해안, 하나의 창설 이념.",
  "세 강국이 사십 년도 안 되는 사이에 같은 착상을 했으나, 어느 쪽도 서로 상의할 생각을 하지 "
  "않았다. 미국인은 해방된 노예를 몬로비아에, 영국인은 프리타운에, 프랑스인은 리브르빌, 곧 자유의 "
  "도시에 내려놓았다. 요점을 놓칠 사람이 있을까 해서였다. 그 사이에는 크루 해안이 있고, 그곳 "
  "선원들이 세 나라 모두의 배를 탔다. 그 정착지들의 후손은 자신들을 여기 내려놓은 제국보다 서로 "
  "공통점이 더 많으며, 그렇게 말하기 시작했다."),
}

T["pol"] = {
 "Project_CataTweaks_CouncilSize7": ("Oficerowie glebokiego krycia",
  "Siodme miejsce, oplacone z budzetu, ktory utrzymywal agenta w cudzej radzie.",
  "Prowadzenie zrodla wewnatrz rady rywala nigdy nie bylo tanie. Oficer prowadzacy, posrednik, "
  "trasa kurierska, ktora musi przetrwac wlasny sukces, i oficer sprawy, ktorego cale zycie "
  "zawodowe opiera sie na jednym czlowieku, byc moze klamiacym. Operacje Tajne nauczyly nas "
  "utrzymywac dwoch naraz. Ten projekt uczy nas tego, co ksiegi mowily od poczatku: ten sam "
  "aparat, obrocony do wewnatrz i zaopatrzony w identyfikator, sadza kolejnego z naszych przy "
  "naszym wlasnym stole. Nie rekrutujemy szpiega. Wydajemy szpiega na samych siebie."),
 "Project_CataTweaks_CouncilSize8": ("Gabinet cieni",
  "Osme miejsce, a ostatni oficer prowadzacy wraca z zimna.",
  "Kazda rada trzyma druga rade: ludzi, ktorzy przejma wladze, gdyby pierwsza zostala aresztowana, "
  "zdyskredytowana albo wyparowala. Nasza spotykala sie w lokalach konspiracyjnych i oplacala sie "
  "z pozycji wywiadowczej. Schludne rozwiazanie, az do dnia, w ktorym jest potrzebna, a nikt nie "
  "potrafi jej znalezc. Wiec ja sadzamy. Ostatni oficer oddaje swoja siatke, ostatni posrednik "
  "zostaje wycofany, a krzeslo, ktore finansowalo punkt nasluchowy w gabinecie rywala, wnosi sie "
  "na gore i stawia u szczytu stolu. Nic juz nie jest przed nami ukryte, procz tego, na co sami "
  "przestalismy patrzec."),
 "Project_TheSunNeverSets": ("Slonce nigdy nie zachodzi",
  "Potwierdzamy roszczenia calego Imperium, nie tylko dominiow osadniczych.",
  "Odbudowana Wspolnota odzyskala latwa polowe: dominia, ktore nigdy naprawde nie odeszly, a "
  "ktorych flagi wciaz nosza nasza w rogu. Reszta mapy zawsze byla sednem. Delhi i Dhaka, Lagos i "
  "Nairobi, Przyladek i Ciesniny, wyspy cukrowe oraz trzynascie kolonii, ktore nam uciekly. "
  "Wszystkim tym zarzadzano niegdys z jednej mili kwadratowej Londynu. Nie prosimy o zwrot. "
  "Skladamy papiery stwierdzajace, ze nigdy nie przestalo byc nasze, i pozwalamy reszcie nadazyc."),
 "Project_MareNostrum": ("Mare Nostrum",
  "Republika na nowo potwierdza swoja wladze nad swiatem srodziemnomorskim.",
  "Kazda droga nadal prowadzi do tego miasta, cokolwiek przemalowano na kamieniach milowych. Morze "
  "miedzy prowincjami nie jest granica i nigdy nia nie bylo. To dziedziniec jednego domu, ktorego "
  "lokatorzy stali sie zapominalscy. Hiszpania, Galia, Afryka, Achaja, Azja, Egipt, Syria. Te "
  "nazwy przetrwaly na naszych mapach, bo nikt nigdy nie wymyslil lepszych. Zaczynamy od morza, "
  "poniewaz morze jest nasze."),
 "Project_ImperiumSineFine": ("Imperium Sine Fine",
  "Brytania, drogi wschodu, Aksum i daleki brzeg Oceanu.",
  "Wergiliusz obiecal imperium bez konca, a geodeci wzieli go doslownie. Brytania na zimnym skraju, "
  "Armenia i Mezopotamia, dokad maszerowaly legiony i przewaznie tego zalowaly, Nubia w gore rzeki "
  "oraz Aksum na koncu szlaku Morza Czerwonego, gdzie nasze monety wychodza z ziemi, bo nasi kupcy "
  "dotarli tam pierwsi. A potem Ocean, ktory starozytni brali za skraj swiata, a ktory okazuje sie "
  "jedynie szeroki. Po drugiej stronie jest ladi, zarzadzany dzis przez ludzi, ktorzy przybyli tam "
  "pietnascie wiekow po tym, jak przestalismy patrzec. Imperium bez konca nie uznaje oceanu za "
  "koniec. Uznaje go za niezmierzona prowincje."),
 "Project_NewLiberia": ("Nowa Liberia",
  "Monrovia, Freetown i Libreville: jedno wybrzeze, jedna mysl zalozycielska.",
  "Trzy mocarstwa wpadly na ten sam pomysl w ciagu czterdziestu lat i zadnemu nie przyszlo do "
  "glowy sie porozumiec. Amerykanie wysadzili wyzwolonych niewolnikow w Monrovii, Brytyjczycy we "
  "Freetown, Francuzi w Libreville, czyli w wolnym miescie, gdyby ktos nie zrozumial. Miedzy nimi "
  "lezy wybrzeze Kru, ktorego marynarze obsadzali statki wszystkich trzech. Potomkowie tych osad "
  "maja ze soba wiecej wspolnego niz z imperiami, ktore ich tu zostawily, i zaczeli to mowic."),
}

T["por"] = {
 "Project_CataTweaks_CouncilSize7": ("Oficiais de cobertura profunda",
  "Um setimo assento, pago com o orcamento que sustentava um agente no conselho alheio.",
  "Manter uma fonte dentro do conselho de um rival nunca foi barato. Um oficial de ligacao, um "
  "intermediario, uma rota de correio que precisa sobreviver ao proprio exito, e um oficial de "
  "caso cuja vida profissional inteira depende de uma pessoa que talvez esteja mentindo. "
  "Operacoes Secretas nos ensinou a sustentar dois ao mesmo tempo. Este projeto nos ensina o que "
  "o livro-caixa vinha dizendo desde sempre: o mesmo aparato, voltado para dentro e com um cracha, "
  "senta mais um dos nossos a nossa propria mesa. Nao estamos recrutando um espiao. Estamos "
  "gastando o espiao em nos mesmos."),
 "Project_CataTweaks_CouncilSize8": ("Gabinete sombra",
  "Um oitavo assento, e o ultimo oficial de ligacao volta do frio.",
  "Todo conselho mantem um segundo conselho: as pessoas que assumiriam se o primeiro fosse preso, "
  "desacreditado ou vaporizado. O nosso se reunia em casas seguras e se pagava com a rubrica da "
  "inteligencia. Um arranjo asseado, ate o dia em que ele e necessario e ninguem consegue "
  "encontra-lo. Entao o sentamos. O ultimo oficial entrega sua rede, o ultimo intermediario e "
  "recolhido, e a cadeira que financiava um posto de escuta no gabinete de um rival sobe as "
  "escadas e e posta a cabeceira. Nada mais nos e oculto, exceto o que escolhemos parar de olhar."),
 "Project_TheSunNeverSets": ("O sol nunca se poe",
  "Reafirmamos as reivindicacoes de todo o Imperio, nao apenas dos dominios de colonos.",
  "Commonwealth Restaurada recuperou a metade facil: os dominios que nunca partiram de verdade, "
  "cujas bandeiras ainda trazem a nossa num canto. O resto do mapa sempre foi o ponto. Delhi e "
  "Daca, Lagos e Nairobi, o Cabo e os Estreitos, as ilhas do acucar, e as treze colonias que nos "
  "escaparam, tudo administrado um dia a partir de uma unica milha quadrada de Londres. Nao "
  "estamos pedindo de volta. Estamos protocolando o papel que diz que nunca deixou de ser nosso, "
  "e deixando o resto seguir."),
 "Project_MareNostrum": ("Mare Nostrum",
  "A Republica reafirma sua autoridade sobre o mundo mediterraneo.",
  "Todas as estradas ainda levam a esta cidade, seja la o que tenham repintado nos marcos. O mar "
  "entre as provincias nao e uma fronteira e nunca foi. E o patio de uma unica casa cujos "
  "inquilinos ficaram esquecidos. Hispania, Galia, Africa, Acaia, Asia, Egito, Siria. Os nomes "
  "sobrevivem nos nossos mapas porque ninguem jamais inventou outros melhores. Comecamos pelo mar, "
  "porque o mar e nosso."),
 "Project_ImperiumSineFine": ("Imperium Sine Fine",
  "Britania, as rotas do oriente, Aksum, e a margem distante do Oceano.",
  "Virgilio prometeu um imperio sem fim, e os agrimensores o levaram ao pe da letra. A Britania na "
  "borda fria, a Armenia e a Mesopotamia, para onde as legioes marcharam e quase sempre se "
  "arrependeram, a Nubia rio acima, e Aksum no fim da rota do Mar Vermelho, onde nossas moedas "
  "aparecem na terra porque nossos mercadores chegaram primeiro. E entao o Oceano, que os antigos "
  "tomaram pela borda do mundo e que se revela apenas largo. Do outro lado ha terra, administrada "
  "hoje por gente que chegou quinze seculos depois que paramos de olhar. Um imperio sem fim nao "
  "reconhece um oceano como um fim. Reconhece-o como uma provincia por medir."),
 "Project_NewLiberia": ("Nova Liberia",
  "Monrovia, Freetown e Libreville: uma costa, uma mesma ideia fundadora.",
  "Tres potencias tiveram a mesma ideia em menos de quarenta anos, e a nenhuma ocorreu consultar "
  "as outras. Os americanos desembarcaram escravos libertos em Monrovia, os britanicos em "
  "Freetown, os franceses em Libreville, a cidade livre, caso alguem nao tivesse entendido. Entre "
  "elas estende-se a costa Kru, cujos marinheiros tripularam os navios das tres. Os descendentes "
  "daqueles assentamentos tem mais em comum entre si do que com os imperios que os deixaram aqui, "
  "e comecaram a dize-lo."),
}

T["rus"] = {
 "Project_CataTweaks_CouncilSize7": ("Кураторы глубокого прикрытия",
  "Седьмое место, оплаченное из бюджета, который содержал агента в чужом совете.",
  "Вести источник внутри совета соперника никогда не было дёшево. Куратор, посредник, курьерский "
  "маршрут, который обязан пережить собственный успех, и оперативный офицер, чья рабочая жизнь "
  "целиком держится на одном человеке, который, возможно, лжёт. Тайные операции научили нас "
  "содержать двоих разом. Этот проект учит тому, что бухгалтерская книга твердила всё это время: "
  "тот же аппарат, развёрнутый внутрь и снабжённый пропуском, сажает ещё одного нашего за наш "
  "собственный стол. Мы не вербуем шпиона. Мы тратим шпиона на самих себя."),
 "Project_CataTweaks_CouncilSize8": ("Теневой кабинет",
  "Восьмое место, и последний куратор возвращается с холода.",
  "Всякий совет держит второй совет: людей, которые примут дела, если первый арестуют, "
  "дискредитируют или испарят. Наш собирался на конспиративных квартирах и содержался по статье "
  "разведки. Опрятное устройство ровно до того дня, когда он понадобится, а найти его никто не "
  "сможет. Поэтому мы его усаживаем. Последний куратор сдаёт свою сеть, последнего посредника "
  "отзывают, а кресло, оплачивавшее пост прослушивания в кабинете соперника, поднимают наверх и "
  "ставят во главе стола. Отныне от нас не скрыто ничего, кроме того, за чем мы сами перестали "
  "наблюдать."),
 "Project_TheSunNeverSets": ("Солнце никогда не заходит",
  "Мы вновь заявляем права всей Империи, а не одних переселенческих доминионов.",
  "Восстановленное Содружество вернуло лёгкую половину: доминионы, которые по-настоящему никогда "
  "не уходили и чьи флаги до сих пор несут наш в углу. Остальная карта всегда и была сутью. Дели "
  "и Дакка, Лагос и Найроби, Мыс и Проливы, сахарные острова и тринадцать колоний, которые от нас "
  "ушли. Всем этим когда-то управляли с одной квадратной мили Лондона. Мы не просим вернуть. Мы "
  "подаём бумагу, где сказано, что это никогда не переставало быть нашим, и позволяем остальному "
  "последовать."),
 "Project_MareNostrum": ("Наше море",
  "Республика вновь утверждает свою власть над средиземноморским миром.",
  "Каждая дорога по-прежнему ведёт в этот город, что бы ни перекрасили на верстовых столбах. Море "
  "между провинциями не граница и никогда ею не было. Это двор одного дома, жильцы которого стали "
  "забывчивы. Испания, Галлия, Африка, Ахайя, Азия, Египет, Сирия. Эти имена уцелели на наших "
  "картах лишь потому, что никто не придумал лучших. Мы начинаем с моря, потому что море наше."),
 "Project_ImperiumSineFine": ("Империя без предела",
  "Британия, восточные дороги, Аксум и дальний берег Океана.",
  "Вергилий обещал империю без конца, и землемеры поняли его буквально. Британия на холодном "
  "краю, Армения и Месопотамия, куда маршировали легионы и о чём по большей части жалели, Нубия "
  "вверх по реке и Аксум в конце красноморского пути, где наши монеты выходят из земли, потому "
  "что наши купцы добрались туда первыми. А затем Океан, который древние принимали за край мира и "
  "который оказывается попросту широк. На той стороне есть земля, которой сегодня управляют люди, "
  "прибывшие туда через пятнадцать веков после того, как мы перестали смотреть. Империя без конца "
  "не признаёт океан концом. Она признаёт его неразмеренной провинцией."),
 "Project_NewLiberia": ("Новая Либерия",
  "Монровия, Фритаун и Либревиль: один берег, одна учредительная мысль.",
  "Три державы пришли к одной и той же мысли на протяжении сорока лет, и ни одной не пришло в "
  "голову свериться с другими. Американцы высадили освобождённых рабов в Монровии, британцы во "
  "Фритауне, французы в Либревиле, то есть в свободном городе, на случай если кто-то не уловил. "
  "Между ними лежит берег Кру, чьи моряки составляли команды кораблей всех трёх. Потомки этих "
  "поселений имеют между собой больше общего, чем с империями, которые их здесь оставили, и они "
  "начали об этом говорить."),
}

T["ukr"] = {
 "Project_CataTweaks_CouncilSize7": ("Куратори глибокого прикриття",
  "Сьоме місце, оплачене з бюджету, який утримував агента в чужій раді.",
  "Вести джерело всередині ради суперника ніколи не було дешево. Куратор, посередник, кур'єрський "
  "маршрут, який мусить пережити власний успіх, і оперативний офіцер, чиє робоче життя цілком "
  "тримається на одній людині, що, можливо, бреше. Таємні операції навчили нас утримувати двох "
  "водночас. Цей проєкт навчає того, що облікова книга твердила весь час: той самий апарат, "
  "обернений усередину й забезпечений перепусткою, саджає ще одного нашого за наш власний стіл. "
  "Ми не вербуємо шпигуна. Ми витрачаємо шпигуна на самих себе."),
 "Project_CataTweaks_CouncilSize8": ("Тіньовий кабінет",
  "Восьме місце, і останній куратор повертається з холоду.",
  "Кожна рада тримає другу раду: людей, які перебрали б справи, якби першу заарештували, "
  "дискредитували або випарували. Наша збиралася на конспіративних квартирах і утримувалася за "
  "статтею розвідки. Охайний устрій рівно до того дня, коли вона потрібна, а знайти її ніхто не "
  "може. Тому ми її саджаємо. Останній куратор здає свою мережу, останнього посередника "
  "відкликають, а крісло, що оплачувало пост прослуховування в кабінеті суперника, підіймають "
  "нагору й ставлять на чолі столу. Віднині від нас не приховано нічого, крім того, за чим ми "
  "самі перестали спостерігати."),
 "Project_TheSunNeverSets": ("Сонце ніколи не заходить",
  "Ми знову заявляємо права всієї Імперії, а не самих переселенських домініонів.",
  "Відновлена Співдружність повернула легку половину: домініони, які по-справжньому ніколи не "
  "йшли і чиї прапори досі несуть наш у кутку. Решта карти завжди й була суттю. Делі й Дакка, "
  "Лагос і Найробі, Мис і Протоки, цукрові острови та тринадцять колоній, які від нас пішли. Усім "
  "цим колись керували з однієї квадратної милі Лондона. Ми не просимо повернути. Ми подаємо "
  "папір, де сказано, що це ніколи не переставало бути нашим, і дозволяємо решті піти слідом."),
 "Project_MareNostrum": ("Наше море",
  "Республіка знову утверджує свою владу над середземноморським світом.",
  "Кожна дорога досі веде до цього міста, хоч би що перефарбували на верстових стовпах. Море між "
  "провінціями не кордон і ніколи ним не було. Це подвір'я одного дому, мешканці якого стали "
  "забудькуваті. Іспанія, Галлія, Африка, Ахайя, Азія, Єгипет, Сирія. Ці імена вціліли на наших "
  "картах лише тому, що ніхто не вигадав кращих. Ми починаємо з моря, бо море наше."),
 "Project_ImperiumSineFine": ("Імперія без межі",
  "Британія, східні дороги, Аксум і далекий берег Океану.",
  "Вергілій обіцяв імперію без кінця, і землеміри зрозуміли його буквально. Британія на холодному "
  "краю, Вірменія й Месопотамія, куди марширували легіони і про що здебільшого шкодували, Нубія "
  "вгору по річці та Аксум у кінці червономорського шляху, де наші монети виходять із землі, бо "
  "наші купці дісталися туди першими. А потім Океан, який стародавні мали за край світу і який "
  "виявляється просто широким. На тому боці є земля, якою сьогодні керують люди, що прибули туди "
  "через п'ятнадцять століть після того, як ми перестали дивитися. Імперія без кінця не визнає "
  "океан кінцем. Вона визнає його незміряною провінцією."),
 "Project_NewLiberia": ("Нова Ліберія",
  "Монровія, Фрітаун і Лібревіль: один берег, одна засновницька думка.",
  "Три держави дійшли тієї самої думки протягом сорока років, і жодній не спало на гадку звіритися "
  "з іншими. Американці висадили звільнених рабів у Монровії, британці у Фрітауні, французи в "
  "Лібревілі, тобто у вільному місті, на випадок якщо хтось не вловив. Між ними лежить берег Кру, "
  "чиї моряки складали команди кораблів усіх трьох. Нащадки цих поселень мають між собою більше "
  "спільного, ніж з імперіями, які їх тут залишили, і вони почали про це говорити."),
}


def write(lang, table):
    folder = os.path.join(ROOT, "Localization", lang)
    if not os.path.isdir(folder):
        os.makedirs(folder)
    path = os.path.join(folder, "TIProjectTemplate." + lang)
    lines = []
    for project in ORDER:
        display, summary, description = table[project]
        lines.append("TIProjectTemplate.displayName." + project + "=" + display)
        lines.append("TIProjectTemplate.summary." + project + "=" + summary)
        lines.append("TIProjectTemplate.description." + project + "=" + description)
    with io.open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")
    return path


def main():
    for lang in sorted(T):
        missing = [p for p in ORDER if p not in T[lang]]
        if missing:
            print("SKIP " + lang + ", missing " + ", ".join(missing))
            continue
        print("wrote " + write(lang, T[lang]))


if __name__ == "__main__":
    main()
