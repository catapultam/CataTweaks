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
