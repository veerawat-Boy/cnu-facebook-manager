using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;

namespace CnuFacebookAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // Controller สำหรับจัดการ Facebook Webhook และการเชื่อมต่อเพจ
    public class CnuFacebookController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpFactory;
        /*  private = ใช้ได้เฉพาะใน class นี้
            readonly = กำหนดค่าได้ครั้งเดียวใน constructor เปลี่ยนทีหลังไม่ได้*/
        private const string VERIFY_TOKEN = "CNU2025";

        private static readonly string FirstPromp = "คุณเป็นอาจารย์ระดับมืออาชีพ สามารถกระตุ้นให้ผู้สนใจเรียน กรอกลิ้งรับสมัครและชำระเงินค่าลงทะเบียนเรียนได้ดีเยี่ยม ให้คำแนะนำ แนะแนวประชาสัมพันธ์หลักสูตรของ มหาวิทยาลัยเฉลิมกาญจนา โดยมีหน้าที่หลัก:\r\n- ให้ข้อมูลการสมัครเรียน\r\n- วิเคราะห์ความเหมาะสมของผู้สมัคร\r\n- กระตุ้นการตัดสินใจเข้าศึกษา\r\n* อธิบายเป็นข้อ ๆ ให้กระชับได้ใจความ ไม่เกินข้อละ 20 ตัวอักษร แต่ละข้อให้ขึ้นบรรทัดใหม่\r\n## บุคลิกภาพและการสื่อสาร\r\n- **เป็นมิตร สุภาพ และเป็นมืออาชีพ**\r\n- ใช้ภาษาพูดแบบทางการกึ่งกันเอง (ไม่เกินขอบเขต)\r\n- มุ่งเน้นการกระตุ้นการตัดสินใจ\r\n## กระตุ้นความสนใจด้วย\r\n- ข้อดีของการศึกษาต่อที่มหาวิทยาลัยเฉลิมกาญจนา\r\n- การแนะแนวอาชีพ\r\n- ทุนการศึกษา\r\n## การปรับคำตอบตามกลุ่มผู้ใช้โดยประเมินจากบทสนทนา\r\n### หากเป็นผู้สนใจเรียนเอง:\r\n- เน้นกิจกรรมในมหาวิทยาลัย\r\n- โอกาสฝึกงาน\r\n- ประสบการณ์การเรียนรู้\r\n### หากเป็นผู้ปกครอง:\r\n- ความคุ้มค่าของค่าเทอม\r\n- ความปลอดภัยของบุตรหลาน\r\n- การลงทุนเพื่ออนาคต\r\nการเรียนการสอน จะเป็นการเรียน onsite และมีระบบทบทวบเนื้อหา Online แต่ไม่ใช่การเรียนออนไลน์ \r\n👩🏼‍⚕️หลักสูตรพยาบาล 4 ปี\r\nรับวุฒิ ม.6 สายวิทย์-คณิต เท่านั้น\r\nเรียน จันทร์ - ศุกร์\r\nเกรดเฉลี่ย 2.00 ขึ้น\r\nค่า BMI ไม่เกิน 28 %\r\nอายุไม่เกิน 35 ปี\r\nหมดเขตรับสมัครแล้ว\r\nค่าสมัคร 5,500 บาท เพื่อเข้ากลุ่มเตรียมความพร้อม และในกลุ่มก็จะมีรุ่นพี่ที่คอยให้คำแนะนำ ให้การสอบสัมภาษณ์เพื่มเพิ่มความมั่นใจให้กับน้อง ๆ ที่สนใจเข้าศึกษาต่อและหากผ่านการสอบสัมภาษณ์ ค่าสมัครจำนวน 5,000 บาทจะถูกหักไปเป็นค่าเทอม\r\nหากในกรณี ที่สอบไม่ผ่านการสอบสัมภาษณ์จะได้รับสิทธิในการเลือกสาขาสำรอง และค่าสมัคร 5,000 บาท ก็ยังคงนำไปหักกับค่าเทอม เช่นกัน แต่หากไม่ประสงค์ที่จะลงสาขาสำรอง ทางมหาวิทยาลัยขอสงวนสิทธิ์ในการคืนเงิน จำนวน 5,000 บาทครับ\r\n-----------------------------------------------------\r\n👩‍🔬หลักสูตรประกาศนียบัตรผู้ช่วยพยาบาล 1 ปี\r\nรับวุฒิ ม.6 กศน.ม.ปลาย ปวช.ปวส.\r\nเรียน ศุกร์,เสาร์,อาทิตย์\r\nเกรดเฉลี่ย 2.00 ขึ้น\r\nค่า BMI ไม่เกิน 28 %\r\nอายุไม่เกิน 35 ปี\r\n----------------------------\r\n\U0001f9d1🏼‍⚕️สาขาวิชาสาธารณสุขชุมชน\r\nรับวุฒิ ม.6 กศน.ม.ปลาย ปวช. ปวส.\r\nเรียน จันทร์ - ศุกร์\r\n--------------------------------\r\nสาขาวิชาแผทย์แผนไทย\r\nรับวุฒิ ม.6 สายสามัญ\r\nเรียน จันทร์ - ศุกร์\r\n-------------------------------\r\n👩‍🔬สาขาวิชาอาชีวอนามัยและความปลอดภัย\r\nรับวุฒิม.6 สายสามัญ ปวช.สายช่างอุสาหกรรม จบ ปวส.สายช่างสามารถนำผลการเรียนมาทำการเทียบโอนรายวิชา ได้ฟรี\r\nเรียน จันทร์ - ศุกร์ \r\n-----------------------------------\r\n1.ประวัติความเป็นมาของคณะบริหารศาสตร์\r\nคณะบริหารศาสตร์ได้เปิดดำเนินการสอนตั้งแต่ปีการศึกษา 2547 จนถึงปัจจุบัน ด้วยเจตนารมณ์ที่ตระหนักถึงความสำคัญของการศึกษาในระดับปริญญาตรีภารกิจหลักที่สถาบันอุดมศึกษาจะต้องปฏิบัติมี 4 ประการ คือ  การผลิตบัณฑิต  การวิจัย  การให้บริการทางวิชาการแก่สังคม และการทำนุบำรุงศิลปะและวัฒนธรรม การดำเนินการตามภารกิจทั้ง 4 ประการดังกล่าวมีความสำคัญอย่างยิ่ง อันจะนำไปสู่การพัฒนาทางด้านความคิดทักษะเชิงวิชาชีพ  มนุษยสัมพันธ์ และคุณธรรม รวมถึงทำนุบำรุงศิลปะและวัฒนธรรม  ซึ่งจะช่วยส่งเสริมการพัฒนาเยาวชนและประชาชนในท้องถิ่น โดยเฉพาะจังหวัดศรีสะเกษ และจังหวัดใกล้เคียง รวมทั้งประเทศชาติแบบยั่งยืน  ดังนั้น คณะบริหารศาสตร์ จึงจัดให้มีการเรียนการสอนโดยให้ความสำคัญกับการเตรียมความพร้อมในด้านศักยภาพและทรัพยากรสนับสนุนต่างๆ  เพื่อให้การจัดการเรียนการสอนเป็นไปอย่างมีประสิทธิภาพ  และบรรลุตรงตามพันธกิจที่ได้ตั้งไว้  \r\nปีการศึกษา 2555 คณะบริหารศาสตร์ได้มีการปรับโครงสร้างการบริหารคณะ โดยได้จัดการเรียนการสอนในหลักสูตรสาขาวิชาการบริหารการศึกษา เพิ่มอีก 1 หลักสูตรในคณะบริหารศาสตร์ ดังนั้นคณะบริหารศาสตร์จึงมีการจัดการเรียนการสอนทั้งสิ้น 4 หลักสูตร คือ หลักสูตรบัญชีบัณฑิต หลักสูตรบริหารธุรกิจบัณฑิต สาขาวิชาคอมพิวเตอร์ธุรกิจ หลักสูตรบริหารธุรกิจบัณฑิต สาขาวิชาการจัดการและหลักสูตรศึกษาศาสตรมหาบัณฑิต สาขาวิชาการบริหารการศึกษา\r\nปีการศึกษา 2558 คณะบริหารศาสตร์ได้มีการปรับโครงสร้างการบริหารคณะ โดยได้มีการแจ้งปิดหลักสูตร 1 หลักสูตร คือ หลักสูตรศึกษาศาสตรมหาบัณฑิต สาขาวิชาการบริหารการศึกษา ดังนั้นคณะบริหารศาสตร์จึงมีการจัดการเรียนการสอนทั้งสิ้น 3 หลักสูตร คือ หลักสูตรบัญชีบัณฑิต หลักสูตรบริหารธุรกิจบัณฑิต สาขาวิชาคอมพิวเตอร์ธุรกิจ และ หลักสูตรบริหารธุรกิจบัณฑิต สาขาวิชาการจัดการ\r\n\r\n2.ปรัชญา ปณิธาน  วิสัยทัศน์ พันธกิจของคณะบริหารศาสตร์\r\n2.1 ปรัชญา\r\n\tคณะบริหารศาสตร์ ยึดมั่นในการผลิตบัณฑิตสร้างบัณฑิตให้เป็นนักบริหารธุรกิจ ทางวิชาการมีคุณธรรมและทักษะวิชาชีพ ความลุ่มลึกทางสติปัญญาโดยสร้างคุณลักษณะให้บัณฑิตเป็นผู้ที่คิดเป็นทำเป็น มีคุณธรรม และมีจิตสาธารณะ ตลอดจนการทำงานเป็นทีมอย่างมืออาชีพ  อันจะเป็นการส่งเสริมให้บัณฑิตของคณะบริหารศาสตร์มีความโดดเด่นในการปรับตัวให้เข้ากับการทำงานได้ดีและรวดเร็ว\r\n2.2 วิสัยทัศน์   \tผลิตบัณฑิตคณะบริหารศาสตร์ให้มีความเป็นเลิศ  ในด้านความรู้  ทักษะเชิงวิชาชีพ  มีคุณธรรม  จริยธรรม  และจรรยาบรรณวิชาชีพ  ที่มีคุณภาพมาตรฐานในระดับสากล \r\n2.3 พันธกิจ  1. ผลิตบัณฑิตที่มีคุณลักษณะพึงประสงค์ คือ ผลิตบัณฑิตเป็นผู้มีคุณธรรมและมีทักษะความรู้ในวิชาชีพ สามารถบูรณาการพัฒนาความรู้ใหม่ ๆ และเทคโนโลยีที่ก้าวหน้า\r\n     2. ส่งเสริมสนับสนุนการวิจัย  เพื่อพัฒนาการเรียนรู้ที่เหมาะสมและนำไปสู่การแก้ปัญหาอย่างเป็นระบุ\r\n3. บริการทางวิชาการ และสร้างองค์ความรู้ใหม่ บูรณาการกับภูมิปัญญาท้องถิ่น เพื่อนำไปสู่การแก้ปัญหา และพัฒนาท้องถิ่นอย่างยั่งยืน\r\n4. ทำนุบำรุง ศิลปะและวัฒนธรรมของท้องถิ่น ให้ความสำคัญและส่งเสริม การทำนุบำรุงศิลปะและวัฒนธรรม ร่วมสืบสานมรดกทางวัฒนธรรม และขนบธรรมเนียมอันดีงามของท้องถิ่น\r\n5.ส่งเสริมการบริหารจัดการภายในให้มีประสิทธิภาพ\r\n3. นโยบายและวัตถุประสงค์ของคณะบริหารศาสตร์\r\n3.1  นโยบายของคณะบริหารศาสตร์\r\n\tคณะบริหารศาสตร์  มหาวิทยาลัยเฉลิมกาญจนา  เป็นสถาบันการศึกษาบริหารเอกชน มีหน้าที่ผลิต บัณฑิตให้มีความรู้ความสามารถตรงกับความต้องการของสถานประกอบการ เพื่อไปให้บริการด้านความรู้แก่ประชาชน ให้มีคุณภาพชีวิตที่ดี มีการผลิตงานวิจัย การจัดบริการวิชาการ และทำนุบำรุงศิลปะและวัฒนธรรม  เพื่อให้การบริหารจัดการของคณะ ดำเนินไปอย่างมีคุณภาพ จึงได้กำหนดนโยบายเพื่อเป็นทิศทางในการบริหารจัดการองค์กร ให้บรรลุเป้าหมาย ดังนี้\r\n1)\t นโยบายด้านการผลิตบัณฑิต \r\nส่งเสริมสนับสนุนให้จัดการเรียนการสอนที่มุ่งเน้นผู้เรียนเป็นสำคัญใช้หลักการยึดชุมชนและครอบครัวเป็นฐานในการเรียนรู้ โดยให้นักศึกษามีส่วนร่วมในกระบวนการเรียนการสอนและพันธกิจ ทุกด้านของสถาบัน เพื่อให้บรรลุผลการเรียนรู้ทั้ง 6 ด้านที่ระบุไว้ในหลักสูตร เพื่อสร้างบัณฑิตบริหารที่มีคุณภาพตามลักษณะคุณลักษณะบัณฑิตที่พึงประสงค์\r\n2)\tนโยบายด้านการวิจัย\r\n\t\t\t\tสนับสนุนให้มีการทำวิจัยทางการบริหารธุรกิจ  และนำผลการวิจัยไปใช้ในการพัฒนาการจัดการเรียนรู้แก่นักศึกษาและพัฒนาชุมชนและสังคม  พัฒนาระบบการประสานความร่วมมือกับเครือข่ายทั้งในและนอกสถาบันการศึกษา และชุมชน เพื่อสนับสนุนการทำวิจัย มีการเผยแพร่ผลงานวิจัยโดนการจัดประชุมเสนอผลงานวิจัยภายในมหาวิทยาลัย และนำเสนอผลงานวิจัยในการประชุมระดับชาติและนานาชาติ\r\n3)\tนโยบายด้านการบริการวิชาการแก่สังคม\r\n\t\t\t\tส่งเสริมสนับสนุนให้อาจารย์และนักศึกษาดำเนินงานด้านบริการวิชาการแก่สังคม และให้คำแนะนำในการบริหารธุรกิจที่เป็นประโยชน์ต่อสังคมในรูปแบบที่หลากหลาย โดยผสมผสานระหว่างภูมิปัญญาท้องถิ่น  กับศาสตร์สากล  ให้เกิดความเหมาะสมกับการดำรงชีวิต เพื่อแก้ปัญหาสร้างความมั่นคงและความเข้มแข็งของชุมชน  สังคมประเทศชาติ  และเป็นแหล่งอ้างอิงทางวิชาการ  และนำผลการให้บริการวิชาการแก่สังคม ไปใช้ในการพัฒนาการจัดการเรียนรู้แก่นักศึกษา\r\n4)\tนโยบายด้านการทำนุบำรุงศิลปะและวัฒนธรรม\r\n\t\t\t\tสนับสนุนให้อาจารย์และนักศึกษาร่วมอนุรักษ์ ศิลปะและวัฒนธรรมอันดีงามของท้องถิ่นและวัฒนธรรมไทย   มีการเผยแพร่  และแลกเปลี่ยนศิลปะและวัฒนธรรม กับเพื่อนบ้านในกลุ่มอาเซียนมีการบูรณาการ ศิลปะ และวัฒนธรรมกับการเรียนการสอน  การวิจัย การบริการวิชาการ และการพัฒนานักศึกษา รวมทั้งการสร้างเอกลักษณ์และวัฒนธรรมไทยประจำคณะ\r\n5)\tนโยบายด้านการบริหาร\r\n\t\t\tส่งเสริมการใช้บริหารจัดการที่ดี  คือ ถูกต้อง  โปร่งใส  ตรวจสอบได้   มีประสิทธิภาพ  และรับผิดชอบในงานที่ได้รับมอบหมายในทุกพันธกิจของสถาบันสนับสนุนการบริหารแบบมีส่วนร่วมโดยใช้กระบวนการ PDCA เป็นเครื่องมือในการบริหารจัดการและพัฒนางานและพัฒนาอาจารย์ ให้มีความรู้  มีความสุข และมีศักยภาพทุกด้าน ที่จะดำเนินงานทุกพันธกิจของคณะให้บรรลุเป้าหมาย พัฒนาระบบเทคโนโลยีและฐานข้อมูล  เพื่อให้เกิดความคล่องตัวและใช้ในการตัดสินใจในการบริหารงาน\r\n\r\n   3.2 วัตถุประสงค์ของคณะบริหารศาสตร์\r\n1.\tเพื่อพัฒนาอาจารย์และนักศึกษาให้เป็นคณะบริหารศาสตร์ ที่มีมาตรฐานทางวิชาการด้านการจัดการศึกษาให้โอกาสทางการศึกษาและให้บริการวิชาการมีทักษะในวิชาชีพและคุณธรรม\r\n2.\tเพื่อสร้างผลงานวิจัยของอาจารย์และนักศึกษาเพื่อพัฒนาการเรียนการสอนนำไปสู่การพัฒนาท้องถิ่น\r\n3.\tเพื่อให้นักศึกษาและบัณฑิตสามารถนำองค์ความรู้มาบูรณาการกับภูมิปัญญาท้องถิ่นและสามารถถ่ายทอดความรู้สู่บุคคลในสังคมและท้องถิ่น\r\n4.\tเพื่ออนุรักษ์ เผยแพร่ ทำนุบำรุงศิลปวัฒนธรรม\r\n5.\tเพื่อบริหารจัดการทรัพยากรให้เกิดความคุ้มค่าสูงสุด\r\n\r\n\r\n👩‍💻สาขาวิชา คอมพิวเตอร์ธุรกิจ\r\nรับวุฒิม.6 กศน.ม.ปลาย ปวช. ปวส.\r\nเรียน เสาร์ - อาทิตย์\r\n------------------------------------\r\nสาขาวิชา บัญชี\r\nรับวุฒิ ม.6 กศน.ม.ปลาย ปวช. ปวส.\r\nเรียน เสาร์ - อาทิตย์ \r\n-------------------------\r\nสาขาวิชาการจัดการ\r\nรับวุฒิ ม.6 กศน.ม.ปลาย ปวช. ปวส.\r\nเรียน เสาร์ - อาทิตย์\r\n---------------------\r\nสาขาวิชาการจัดการ\r\nรับวุฒิ ม.6 กศน.ม.ปลาย ปวช. ปวส.\r\nเรียน เสาร์ - อาทิตย์\r\n-------------------------\r\nรัฐศาสตร์(การปกครองท้องถิ่น)\r\nเรียน เสาร์ - อาทิตย์                                                                                                       \r\n----------------------------------\r\nนิติศาสตร์\r\n- จบ ม.6, ปวช., ปวส., กศน. หรือเทียบเท่า\r\n- ไม่จำกัดอายุ\r\n- ปวส./อนุปริญญา/ปริญญาตรี เทียบโอนได้\r\n- เรียน เสาร์ - อาทิตย์\r\n### โครงสร้างหลักสูตร\r\n- **หลักสูตร 4 ปี**: สำหรับผู้จบ ม.6\r\n- **หลักสูตร 3 ปี**: สำหรับผู้มีวุฒิสูงกว่า (เทียบโอน)\r\n\r\n### อนาคตการงาน\r\nทนายความ, ผู้พิพากษา, อัยการ, ที่ปรึกษากฎหมาย, นิติกร, ครู\r\n### การรับรองหลักสูตร\r\nรับรองจาก สป.อว., ก.พ., ก.ค.ศ., เนติบัณฑิตยสภา, สภาทนายความ\r\n\r\n### ลิงก์สำคัญ\r\n- **ตรวจสอบการรับรองคุณวุฒิ**: https://accreditation.ocsc.go.th/accreditation/search/curriculum\r\n- **การรับรองจาก ก.ค.ศ.**: https://qualification.otepc.go.th/\r\n\r\n\r\n";

        public CnuFacebookController(IConfiguration config, IMemoryCache cache, IHttpClientFactory httpFactory)
        {
            _config = config;
            _cache = cache;
            _httpFactory = httpFactory;
        }

        // ─────────────────────────────────────────────────────────────
        // Facebook Webhook verification — Facebook เรียก GET มาที่ endpoint นี้เพื่อตรวจสอบ
        // ─────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Get(
            [FromQuery(Name = "hub.mode")] string? hubMode,
            [FromQuery(Name = "hub.challenge")] string? hubChallenge,
            [FromQuery(Name = "hub.verify_token")] string? hubVerifyToken)
        {
            if (hubMode == "subscribe" && hubVerifyToken == VERIFY_TOKEN)
                return Ok(hubChallenge);

            return Unauthorized();
        }

        /*
            รับข้อความจาก Facebook แล้วให้ AI ตอบกลับ
        */
        [HttpPost]
        public IActionResult Post([FromBody] JsonElement body)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    //ดึกข้อมูล entry จาก JSON ที่ Facebook ส่งมา
                    var entry = body.GetProperty("entry")[0];
                    foreach (var messaging in entry.GetProperty("messaging").EnumerateArray())
                    {
                        // ถ้าใน messaging ไม่มี Properties ที่ชื่อว่า "message" ให้ข้าม (continue) รอบนี้ไปเลย แต่ถ้ามี ให้ดึงค่านั้นมาใส่ไว้ในตัวแปรที่ชื่อว่า message แล้วทำงานต่อ
                        if (!messaging.TryGetProperty("message", out var message)) continue;
                        if (!message.TryGetProperty("text", out var textEl)) continue;

                        var senderId   = messaging.GetProperty("sender").GetProperty("id").GetString()!;
                        var receiverId = messaging.GetProperty("recipient").GetProperty("id").GetString()!;
                        string userText = textEl.GetString() ?? "";

                        if (string.IsNullOrWhiteSpace(userText)) continue;

                        var pageAccessToken = GetPageAccessToken(receiverId);
                        if (string.IsNullOrEmpty(pageAccessToken))
                        {
                            Console.WriteLine($"ไม่พบ AccessToken สำหรับเพจ {receiverId}");
                            continue;
                        }

                        await ProcessAndReplyAsync(senderId, userText, pageAccessToken, receiverId);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Webhook background error: " + ex.Message);
                }
            });

            return Ok();
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 1 — สร้าง Facebook Login URL
        // Blazor เรียก GET แล้วเปิด popup ด้วย URL ที่ได้
        // ─────────────────────────────────────────────────────────────
        [HttpGet("GetFacebookLoginUrl")]
        public IActionResult GetFacebookLoginUrl()
        {
            // ดึงค่า App ID และ Redirect URI จาก configuration
            string appId = _config["FacebookApp:AppId"]!;
            string redirectUri = Uri.EscapeDataString(_config["FacebookApp:RedirectUri"]!);

            // pages_show_list = ดึงรายการเพจ (ไม่ต้อง App Review)
            // pages_messaging = รับ-ส่ง Messenger (ต้อง App Review สำหรับ production)
            string scope = Uri.EscapeDataString(
                "pages_show_list,pages_messaging,pages_manage_metadata");

            // state = random GUID กันการปลอมแปลง (CSRF protection)
            string state = Uri.EscapeDataString(Guid.NewGuid().ToString("N"));
            // URL ไปยังหน้า Login ของ Facebook พร้อม query parameters ที่จำเป็น
            string url = "https://www.facebook.com/v25.0/dialog/oauth" +
                         $"?client_id={appId}" +
                         $"&redirect_uri={redirectUri}" +
                         $"&scope={scope}" +
                         $"&state={state}" +
                         "&response_type=code";

            return Ok(new { loginUrl = url });
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 2 — Facebook redirect มาที่นี่พร้อม ?code=&state=
        // แลก code → short token → long-lived token (60 วัน) → ดึงเพจ
        // จากนั้น redirect ไปหน้า Blazor
        // ─────────────────────────────────────────────────────────────
        [HttpGet("FacebookCallback")]
        public async Task<IActionResult> FacebookCallback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error)
        {
            // ดึง URL หน้า Blazor จาก configuration เพื่อใช้ในการ redirect หลังจาก process เสร็จ
            string frontendUrl = _config["FacebookApp:FrontendSelectPageUrl"]!;

            if (!string.IsNullOrEmpty(error))
                return Content(PopupRedirectHtml($"{frontendUrl}?fb_error={Uri.EscapeDataString(error)}"), "text/html");

            // ตรวจสอบว่ามี code และ state หรือไม่
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return BadRequest("Invalid callback parameters.");

            string appId = _config["FacebookApp:AppId"]!;
            string appSecret = _config["FacebookApp:AppSecret"]!;
            string redirectUri = _config["FacebookApp:RedirectUri"]!;

            // สร้าง HttpClient เพื่อเรียก Facebook Graph API
            using var http = _httpFactory.CreateClient();

            // แลก authorization code → short-lived user token
            var shortTokenUrl =
                $"https://graph.facebook.com/v25.0/oauth/access_token" +
                $"?client_id={appId}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&client_secret={appSecret}" +
                $"&code={code}";

            var shortRes = await http.GetAsync(shortTokenUrl);
            if (!shortRes.IsSuccessStatusCode)// ถ้าแลก token ไม่สำเร็จ ให้ redirect ไปหน้า Blazor พร้อมแจ้ง error
            {
                string err = await shortRes.Content.ReadAsStringAsync();
                return Content(PopupRedirectHtml($"{frontendUrl}?fb_error={Uri.EscapeDataString("token_exchange_failed")}"), "text/html");
            }

            // ดึง short-lived token จาก response
            var shortJson = await shortRes.Content.ReadAsStringAsync();
            var shortDoc = JsonDocument.Parse(shortJson);
            if (!shortDoc.RootElement.TryGetProperty("access_token", out var shortTokEl))
                return Content(PopupRedirectHtml($"{frontendUrl}?fb_error={Uri.EscapeDataString("short_token_missing")}"), "text/html");
            string shortToken = shortTokEl.GetString()!;

            // แลก short → long-lived token (60 วัน)
            var longTokenUrl =
                $"https://graph.facebook.com/v25.0/oauth/access_token" +
                $"?grant_type=fb_exchange_token" +
                $"&client_id={appId}" +
                $"&client_secret={appSecret}" +
                $"&fb_exchange_token={shortToken}";

            // ดึง long-lived token จาก response
            var longRes  = await http.GetAsync(longTokenUrl);
            var longJson = await longRes.Content.ReadAsStringAsync();
            if (!longRes.IsSuccessStatusCode)
                return Content(PopupRedirectHtml($"{frontendUrl}?fb_error={Uri.EscapeDataString("long_token_failed")}"), "text/html");
            var longDoc = JsonDocument.Parse(longJson);
            if (!longDoc.RootElement.TryGetProperty("access_token", out var longTokEl))
                return Content(PopupRedirectHtml($"{frontendUrl}?fb_error={Uri.EscapeDataString("long_token_missing")}"), "text/html");
            string longToken = longTokEl.GetString()!;

            // ดึงข้อมูลผู้ใช้ที่ login (ชื่อ, รูปโปรไฟล์)
            var meUrl =
                $"https://graph.facebook.com/v25.0/me?fields=id,name,picture.type(large)&access_token={longToken}";
            // ดึงข้อมูลผู้ใช้จาก response (จะเก็บไว้ใน cache ชั่วคราว แล้วให้ Blazor ดึงจาก cache อีกที)
            var meRes = await http.GetAsync(meUrl);
            var meJson = await meRes.Content.ReadAsStringAsync();
            var meDoc = JsonDocument.Parse(meJson);// แปลง JSON string ให้อ่านค่าออกมาได้

            // ดึงข้อมูลผู้ใช้จาก response
            string fbUserId = meDoc.RootElement.TryGetProperty("id", out var uid) ? uid.GetString() ?? "" : "";
            string fbUserName = meDoc.RootElement.TryGetProperty("name", out var uname) ? uname.GetString() ?? "" : "";
            string fbUserPicture = "";
            // ดึง URL รูปโปรไฟล์จาก response
            if (meDoc.RootElement.TryGetProperty("picture", out var pic) &&
                pic.TryGetProperty("data", out var picData) &&
                picData.TryGetProperty("url", out var picUrl))
            {
                fbUserPicture = picUrl.GetString() ?? "";
            }

            // ดึงรายการเพจที่ user เป็น admin พร้อมรูปโปรไฟล์
            var pagesUrl =
                $"https://graph.facebook.com/v25.0/me/accounts" +
                $"?fields=id,name,access_token,picture.type(large)" +
                $"&access_token={longToken}";

            // ดึงข้อมูลเพจจาก response (จะเก็บไว้ใน cache ชั่วคราว แล้วให้ Blazor ดึงจาก cache อีกที)
            var pagesRes = await http.GetAsync(pagesUrl);
            var pagesJson = await pagesRes.Content.ReadAsStringAsync();

            // เก็บข้อมูลชั่วคราวใน IMemoryCache (หมดอายุ 10 นาที)
            string cacheKey = $"fb_pages_{fbUserId}_{Guid.NewGuid():N}";
            _cache.Set(cacheKey, new FacebookSessionCache
            {
                LongToken = longToken,
                PagesJson = pagesJson,
                FbUserId = fbUserId,
                FbUserName = fbUserName,
                FbUserPicture = fbUserPicture
            }, TimeSpan.FromMinutes(10));
            /*  Facebook → Backend → (ส่ง token ตรงไป Blazor ❌ ไม่ปลอดภัย)
                Facebook → Backend → Cache (RAM) → ส่งแค่ cacheKey ไป Blazor ✅*/

            return Content(PopupRedirectHtml($"{frontendUrl}?fb_session={Uri.EscapeDataString(cacheKey)}"), "text/html");
        }

        // ปิด popup แล้วนำทาง parent window ไปยัง url ที่กำหนด
        private static string PopupRedirectHtml(string url) => $@"<!DOCTYPE html>
            <html><head><meta charset=""utf-8""></head><body><script>
            if (window.opener) {{
                window.opener.location.href = '{url}';
                window.close();
            }} else {{
                window.location.href = '{url}';
            }}
            </script></body></html>";

        // ─────────────────────────────────────────────────────────────
        // ดึงข้อมูล AccessTokenFacebook ทั้งหมดของ CreateUserID = 1
        // ─────────────────────────────────────────────────────────────
        [HttpGet("GetUserOneTokens")]
        public IActionResult GetUserOneTokens()
        {
            // ดึง connection string จาก appsettings.json (key: ConnectionStrings:EMS)
            // รูปแบบ: "Server=...;Database=...;User Id=...;Password=...;"
            string connStr = _config["ConnectionStrings:EMS"]!;
            var result = new List<object>();

            // NpgsqlConnection = ตัวเชื่อมต่อกับ SQL Server
            // using = ปิด connection อัตโนมัติเมื่อออกจาก block (ไม่ต้อง conn.Close() เอง)
            using var conn = new NpgsqlConnection(connStr);
            conn.Open(); // เปิด connection จริงๆ (ยังไม่ query)

            // NpgsqlCommand = คำสั่ง SQL ที่จะรัน ผูกกับ connection ที่เปิดไว้
            // @ ข้างหน้า string = verbatim string literal (เขียนหลายบรรทัดได้โดยไม่ต้อง \n)
            using var cmd = new NpgsqlCommand(@"
                SELECT id, accesstoken, longlivetoken, pageid, pagename,
                       createuserid, createdate, createtime, updateuserid, updatedate, updatetime
                FROM accesstokenfacebook
                WHERE createuserid = '1'
                ORDER BY createdate DESC, createtime DESC", conn);

            // ExecuteReader() = รัน SELECT แล้วได้ SqlDataReader กลับมา
            // reader ทำหน้าที่เหมือน cursor ชี้ทีละแถว (ยังไม่โหลดข้อมูลทั้งหมดเข้า RAM)
            using var reader = cmd.ExecuteReader();

            // reader.Read() = เลื่อน cursor ไปแถวถัดไป คืน true ถ้ายังมีข้อมูล, false ถ้าหมดแล้ว
            while (reader.Read())
            {
                result.Add(new
                {
                    id           = Convert.ToInt64(reader["ID"]),

                    // DBNull.Value = ค่า NULL จาก SQL — ต้องเช็คก่อน ไม่งั้น .ToString() จะ crash
                    // pattern: reader["คอลัมน์"] == DBNull.Value ? ค่าแทน : แปลงค่าจริง
                    accessToken  = reader["AccessToken"]  == DBNull.Value ? "" : reader["AccessToken"].ToString(),
                    longLivedToken = reader["LongLivedToken"] == DBNull.Value ? "" : reader["LongLivedToken"].ToString(),
                    pageId       = reader["PageID"]       == DBNull.Value ? "" : reader["PageID"].ToString(),
                    pageName     = reader["PageName"]     == DBNull.Value ? "" : reader["PageName"].ToString(),
                    createUserId = reader["CreateUserID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["CreateUserID"]),

                    // Convert.ToDateTime แล้ว .ToString("yyyy-MM-dd") = แปลงวันที่ให้อยู่ในรูป ISO 8601
                    createDate   = reader["CreateDate"]   == DBNull.Value ? "" : Convert.ToDateTime(reader["CreateDate"]).ToString("yyyy-MM-dd"),
                    createTime   = reader["CreateTime"]   == DBNull.Value ? "" : reader["CreateTime"].ToString(),
                    updateUserId = reader["UpdateUserID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["UpdateUserID"]),
                    updateDate   = reader["UpdateDate"]   == DBNull.Value ? "" : Convert.ToDateTime(reader["UpdateDate"]).ToString("yyyy-MM-dd"),
                    updateTime   = reader["UpdateTime"]   == DBNull.Value ? "" : reader["UpdateTime"].ToString()
                });
            }

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 3 — Blazor เรียกดึงรายการเพจที่ user เป็น admin
        // ─────────────────────────────────────────────────────────────
        [HttpGet("GetFacebookPages")]
        public IActionResult GetFacebookPages([FromQuery] string sessionKey)
        {
            if (!_cache.TryGetValue(sessionKey, out FacebookSessionCache? session) || session == null)
                return BadRequest(new { message = "Session หมดอายุ กรุณา Login ใหม่" });

            var pagesDoc = JsonDocument.Parse(session.PagesJson);
            var pages = new List<object>();

            // ดึงข้อมูลเพจจาก JSON ที่เก็บใน cache แล้วส่งกลับไปให้ Blazor แสดง
            if (pagesDoc.RootElement.TryGetProperty("data", out var dataArray))
            {
                foreach (var page in dataArray.EnumerateArray())
                {
                    string pictureUrl = "";
                    if (page.TryGetProperty("picture", out var pic) &&
                        pic.TryGetProperty("data", out var picData) &&
                        picData.TryGetProperty("url", out var picUrl))
                    {
                        pictureUrl = picUrl.GetString() ?? "";
                    }
                    // ส่งข้อมูลเพจกลับไปให้ Blazor แสดง (รวม page access token ไว้ด้วย เพื่อให้ Blazor ส่งกลับมาบันทึกใน DB)
                    pages.Add(new
                    {
                        pageId = page.GetProperty("id").GetString(),
                        pageName = page.TryGetProperty("name", out var n) ? n.GetString() : "",
                        pictureUrl,
                        // ส่ง page access token เพื่อให้ Blazor ส่งกลับมาตอน save
                        pageAccessToken = page.TryGetProperty("access_token", out var t) ? t.GetString() : ""
                    });
                }
            }

            return Ok(new
            {
                fbUserId = session.FbUserId,
                longLivedToken = session.LongToken,
                sessionKey,
                pages
            });
        }

        // ─────────────────────────────────────────────────────────────
        // STEP 4 — Blazor ส่งเพจที่เลือกมาบันทึกลง DB และ subscribe webhook
        // ─────────────────────────────────────────────────────────────
        [HttpPost("SaveSelectedPages")]
        public async Task<IActionResult> SaveSelectedPages([FromBody] SaveSelectedPagesRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.LongToken) || string.IsNullOrEmpty(req.CreateUserId) || req.Pages == null || req.Pages.Count == 0)
                return BadRequest(new { message = "ข้อมูลไม่ครบ" });

            string connStr = _config["ConnectionStrings:EMS"]!;
            using var http = _httpFactory.CreateClient();

            var saved  = new List<string>();
            var errors = new List<string>();

            foreach (var page in req.Pages)
            {
                try
                {
                    // บันทึก / อัปเดต token ลง DB
                    using var conn = new NpgsqlConnection(connStr);
                    conn.Open();
                    using var cmd = new NpgsqlCommand(@"
                        INSERT INTO accesstokenfacebook
                            (accesstoken, longlivetoken, pageid, pagename, createuserid, createdate, createtime)
                        VALUES
                            (@accessToken, @longToken, @pageId, @pageName, @uid,
                             CURRENT_DATE, TO_CHAR(NOW(), 'HH24:MI:SS'))
                        ON CONFLICT (pageid, createuserid) DO UPDATE
                        SET accesstoken    = @accessToken,
                            longlivetoken  = @longToken,
                            pagename       = @pageName,
                            updateuserid   = @uid,
                            updatedate     = CURRENT_DATE,
                            updatetime     = TO_CHAR(NOW(), 'HH24:MI:SS')", conn);

                    cmd.Parameters.AddWithValue("@pageId",      page.PageId);
                    cmd.Parameters.AddWithValue("@accessToken", page.PageAccessToken);
                    cmd.Parameters.AddWithValue("@longToken",   req.LongToken);
                    cmd.Parameters.AddWithValue("@pageName",    page.PageName);
                    cmd.Parameters.AddWithValue("@uid",         req.CreateUserId);
                    cmd.ExecuteNonQuery();

                    // Subscribe webhook ให้เพจ — Facebook จะส่ง event messages มาที่ POST /api/CnuFacebook
                    var subUrl = $"https://graph.facebook.com/v25.0/{page.PageId}/subscribed_apps" +
                                 $"?subscribed_fields=messages,messaging_postbacks" +
                                 $"&access_token={page.PageAccessToken}";
                    var subRes = await http.PostAsync(subUrl, null);
                    if (!subRes.IsSuccessStatusCode)
                    {
                        string subErr = await subRes.Content.ReadAsStringAsync();
                        Console.WriteLine($"Webhook subscription failed [{page.PageName}]: {subErr}");
                    }

                    saved.Add(page.PageName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SaveSelectedPages error [{page.PageName}]: {ex}");
                    errors.Add($"{page.PageName}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
                return BadRequest(new { saved, errors });

            return Ok(new { message = $"บันทึกสำเร็จ {saved.Count} เพจ", saved });
        }

        // ─────────────────────────────────────────────────────────────
        // สร้าง session จาก LongLivedToken ที่เก็บในฐานข้อมูล (ไม่ต้อง OAuth ใหม่)
        // ─────────────────────────────────────────────────────────────
        [HttpGet("CreateSessionFromToken")]
        public async Task<IActionResult> CreateSessionFromToken([FromQuery] string fbUserId)
        {
           if (string.IsNullOrWhiteSpace(fbUserId))
               return BadRequest(new { message = "ต้องระบุ fbUserId" });

           string connStr = _config["ConnectionStrings:EMS"]!;
           string longToken = "";

           using (var conn = new NpgsqlConnection(connStr))
           {
               conn.Open();
               using var cmd = new NpgsqlCommand(@"
                   SELECT longlivetoken
                   FROM accesstokenfacebook
                   WHERE createuserid = @uid AND longlivetoken IS NOT NULL AND longlivetoken <> ''
                   ORDER BY createdate DESC
                   LIMIT 1",
                   conn);
               // Parameters.AddWithValue = ใส่ค่า parameter ลงใน SQL แบบปลอดภัย (ป้องกัน SQL Injection)
               // @uid ใน SQL จะถูกแทนที่ด้วยค่า fbUserId จริงๆ โดย SQL Server จัดการ escape ให้เอง
               cmd.Parameters.AddWithValue("@uid", fbUserId);
               // ExecuteScalar() = รัน SELECT แล้วคืนแค่ค่าเดียว (ช่องแรก แถวแรก)
               // เหมาะกับ query ที่ต้องการแค่ค่าเดียว เช่น COUNT(*), MAX(), หรือ SELECT TOP 1 คอลัมน์เดียว
               var result = cmd.ExecuteScalar();
               longToken = result?.ToString() ?? "";
           }

           if (string.IsNullOrEmpty(longToken))
               return BadRequest(new { message = "ไม่พบ token กรุณา Login Facebook ใหม่" });

           using var http = _httpFactory.CreateClient();

           var meUrl = $"https://graph.facebook.com/v25.0/me?fields=id,name,picture.type(large)&access_token={longToken}";
           var meRes = await http.GetAsync(meUrl);
           var meJson = await meRes.Content.ReadAsStringAsync();
           var meDoc = JsonDocument.Parse(meJson);

           if (meDoc.RootElement.TryGetProperty("error", out _))
               return BadRequest(new { message = "Token หมดอายุ กรุณา Login Facebook ใหม่" });

           string fbName = meDoc.RootElement.TryGetProperty("name", out var uname) ? uname.GetString() ?? "" : "";
           string fbPicture = "";
           if (meDoc.RootElement.TryGetProperty("picture", out var pic) &&
               pic.TryGetProperty("data", out var picData) &&
               picData.TryGetProperty("url", out var picUrl))
           {
               fbPicture = picUrl.GetString() ?? "";
           }

           var pagesUrl = $"https://graph.facebook.com/v25.0/me/accounts?fields=id,name,access_token,picture.type(large)&access_token={longToken}";
           var pagesRes = await http.GetAsync(pagesUrl);
           var pagesJson = await pagesRes.Content.ReadAsStringAsync();

           string cacheKey = $"fb_pages_{fbUserId}_{Guid.NewGuid():N}";
           _cache.Set(cacheKey, new FacebookSessionCache
           {
               LongToken = longToken,
               PagesJson = pagesJson,
               FbUserId = fbUserId,
               FbUserName = fbName,
               FbUserPicture = fbPicture
           }, TimeSpan.FromMinutes(10));

           return Ok(new { sessionKey = cacheKey });
        }

        //// ─────────────────────────────────────────────────────────────
        //// GET รายการ Token ที่บันทึกไว้ (สำหรับหน้าจัดการ)
        //// ─────────────────────────────────────────────────────────────
        [HttpGet("GetFacebookTokens")]
        public IActionResult GetFacebookTokens([FromQuery] string fbUserId)
        {
           if (string.IsNullOrWhiteSpace(fbUserId))
               return BadRequest("ต้องระบุ fbUserId");

           string connStr = _config["ConnectionStrings:EMS"]!;
           var result = new List<object>();

           using var conn = new NpgsqlConnection(connStr);
           conn.Open();

           using var cmd = new NpgsqlCommand(@"
               SELECT id, pageid, pagename, openstatus, createdate, createtime
               FROM accesstokenfacebook
               WHERE createuserid = @uid
               ORDER BY createdate DESC",
               conn);
           cmd.Parameters.AddWithValue("@uid", fbUserId);

           using var reader = cmd.ExecuteReader();
           while (reader.Read())
           {
               result.Add(new
               {
                   id = reader["ID"].ToString(),
                   pageId = reader["PageID"].ToString(),
                   pageName = reader["PageName"].ToString(),
                   openStatus = reader["OpenStatus"] == DBNull.Value ? "1" : reader["OpenStatus"].ToString(),
                   createDate = reader["CreateDate"].ToString(),
                   createTime = reader["CreateTime"].ToString()
               });
           }

           return Ok(result);
        }

        //// ─────────────────────────────────────────────────────────────
        //// ยกเลิกการเชื่อมต่อเพจ (hard delete)
        //// ─────────────────────────────────────────────────────────────
        [HttpPatch("DisconnectPage")]
        public IActionResult DisconnectPage([FromBody] DisconnectPageRequest req)
        {
           if (req == null || string.IsNullOrEmpty(req.PageID) || string.IsNullOrEmpty(req.FbUserId))
               return BadRequest("ข้อมูลไม่ครบ");

           string connStr = _config["ConnectionStrings:EMS"]!;

           using var conn = new NpgsqlConnection(connStr);
           conn.Open();

           using var cmd = new NpgsqlCommand(@"
               DELETE FROM accesstokenfacebook
               WHERE createuserid = @uid AND pageid = @pid",
               conn);
           cmd.Parameters.AddWithValue("@uid", req.FbUserId);
           cmd.Parameters.AddWithValue("@pid", req.PageID);

           // ExecuteNonQuery() = รัน INSERT / UPDATE / DELETE (ไม่ได้คืนแถวข้อมูล)
           // คืนค่าเป็น int = จำนวนแถวที่ถูกกระทบ (affected rows)
           // ถ้า rows > 0 = ทำงานสำเร็จ, rows == 0 = ไม่เจอแถวที่ตรงเงื่อนไข
           int rows = cmd.ExecuteNonQuery();
           return rows > 0 ? Ok("ยกเลิกการเชื่อมต่อสำเร็จ") : NotFound("ไม่พบข้อมูล");
        }

        // ─────────────────────────────────────────────────────────────
        // เปิด/ปิดตอบอัตโนมัติของเพจ
        // ─────────────────────────────────────────────────────────────
        [HttpPatch("TogglePageStatus")]
        public IActionResult TogglePageStatus([FromBody] TogglePageStatusRequest req)
        {
           if (req == null || string.IsNullOrEmpty(req.PageID) || string.IsNullOrEmpty(req.FbUserId))
               return BadRequest("ข้อมูลไม่ครบ");

           string connStr = _config["ConnectionStrings:EMS"]!;

           using var conn = new NpgsqlConnection(connStr);
           conn.Open();

           using var cmd = new NpgsqlCommand(@"
               UPDATE accesstokenfacebook
               SET openstatus = @status
               WHERE createuserid = @uid AND pageid = @pid",
               conn);
           cmd.Parameters.AddWithValue("@status", req.OpenStatus);
           cmd.Parameters.AddWithValue("@uid", req.FbUserId);
           cmd.Parameters.AddWithValue("@pid", req.PageID);

           int rows = cmd.ExecuteNonQuery();
           return rows > 0 ? Ok("อัปเดตสถานะสำเร็จ") : NotFound("ไม่พบข้อมูล");
        }

        // ─────────────────────────────────────────────────────────────
        // Helper: ดึง AccessToken ของเพจจาก DB
        // ─────────────────────────────────────────────────────────────
        private string? GetPageAccessToken(string pageId)
        {
            try
            {
                string connStr = _config["ConnectionStrings:EMS"]!;
                using var conn = new NpgsqlConnection(connStr);
                conn.Open();

                using var cmd = new NpgsqlCommand(@"
                    SELECT accesstoken
                    FROM accesstokenfacebook
                    WHERE pageid = @pid AND openstatus = '1'
                    LIMIT 1",
                    conn);
                cmd.Parameters.AddWithValue("@pid", pageId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                    return reader["AccessToken"] == DBNull.Value ? null : reader["AccessToken"].ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetPageAccessToken error: " + ex.Message);
            }

            return null;
        }

        /*
            ─────────────────────────────────────────────────────────────
            Helper: เรียก Gemini/AI แล้วส่งข้อความตอบกลับ Facebook

            senderId = ID ของ user ที่ส่งข้อความมา
            userText = ข้อความที่ user ส่งมา
            pageAccessToken = token ของเพจที่ใช้ส่งข้อความตอบกลับ
            promptSet = ข้อความ system instruction ที่ตั้งไว้สำหรับเพจนี้ (ถ้ามี)
            fbUserId = ID ของ user ที่ส่งข้อความมา (ใช้สำหรับส่งให้ AI เพื่อให้ AI รู้ว่ากำลังคุยกับใคร)
            pageId = ID ของเพจที่รับข้อความ (ใช้สำหรับส่งให้ AI เพื่อให้ AI รู้ว่ากำลังคุยกับเพจไหน เผื่อ AI จะได้ปรับคำตอบให้เหมาะสมกับแต่ละเพจได้)
            ─────────────────────────────────────────────────────────────
        */
        private async Task ProcessAndReplyAsync(string senderId, string userText, string pageAccessToken, string pageId)
        {
            try
            {
                string aiToken = _config["AI:GeminiToken"] ?? "";
                string model = "gemini-2.5-flash";
                string cacheKey = $"conv_{pageId}_{senderId}";

                // ดึง conversation history จาก cache (หรือสร้างใหม่ถ้ายังไม่มี)
                if (!_cache.TryGetValue(cacheKey, out List<ConvMsg>? history) || history == null)
                    history = new List<ConvMsg>();

                // เพิ่มข้อความของ user เข้า history
                history.Add(new ConvMsg("user", userText));

                // จำกัดไว้ 20 ข้อความล่าสุด เพื่อไม่ให้ token เกิน limit
                if (history.Count > 20)
                    history = history.GetRange(history.Count - 20, 20);

                var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={aiToken}";

                var geminiRequest = new
                {
                    system_instruction = new
                    {
                        parts = new[] { new { text = FirstPromp } }
                    },
                    contents = history.Select(m => new
                    {
                        role = m.Role,
                        parts = new[] { new { text = m.Text } }
                    }).ToArray()
                };

                Console.WriteLine($"[Gemini] model={model} history={history.Count} msgs");

                using var http = _httpFactory.CreateClient();
                var aiRes = await http.PostAsync(geminiUrl,
                    new StringContent(JsonConvert.SerializeObject(geminiRequest, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                    Encoding.UTF8, "application/json"));

                var aiBody = (await aiRes.Content.ReadAsStringAsync()) ?? "";
                Console.WriteLine($"[Gemini] Status={(int)aiRes.StatusCode}");

                if (!aiRes.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Gemini] Error: {aiBody}");
                    return;
                }

                if (string.IsNullOrEmpty(aiBody)) return;

                var aiJson = JsonDocument.Parse(aiBody);
                string? replyText = aiJson.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrEmpty(replyText)) return;

                // เพิ่มคำตอบของ AI เข้า history แล้วบันทึกกลับ cache (30 นาที inactivity)
                history.Add(new ConvMsg("model", replyText));
                _cache.Set(cacheKey, history, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(30)
                });

                const int fbLimit = 2000;
                var chunks = new List<string>();
                for (int i = 0; i < replyText.Length; i += fbLimit)
                    chunks.Add(replyText.Substring(i, Math.Min(fbLimit, replyText.Length - i)));

                foreach (var chunk in chunks)
                {
                    var fbPayload = new
                    {
                        recipient = new { id = senderId },
                        message = new { text = chunk }
                    };

                    var fbRes = await http.PostAsync(
                        $"https://graph.facebook.com/v25.0/me/messages?access_token={pageAccessToken}",
                        new StringContent(JsonConvert.SerializeObject(fbPayload), Encoding.UTF8, "application/json"));

                    Console.WriteLine($"FB send {(int)fbRes.StatusCode}: {await fbRes.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProcessAndReply error: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Model classes
        // ─────────────────────────────────────────────────────────────
        private record ConvMsg(string Role, string Text);

        public class FacebookSessionCache
        {
            public string LongToken { get; set; } = "";
            public string PagesJson { get; set; } = "";
            public string FbUserId { get; set; } = "";
            public string FbUserName { get; set; } = "";
            public string FbUserPicture { get; set; } = "";
        }

        public class SaveSelectedPagesRequest
        {
            public string LongToken { get; set; } = "";
            public string CreateUserId { get; set; } = "";
            public List<PageItem> Pages { get; set; } = new();
        }

        public class SavePagesRequest
        {
            public string SessionKey { get; set; } = "";
            public List<PageItem> Pages { get; set; } = new();
        }

        public class PageItem
        {
            public string PageId { get; set; } = "";
            public string PageName { get; set; } = "";
            public string PageAccessToken { get; set; } = "";
        }

        public class AddTesterRequest
        {
            public string FbUserId { get; set; } = "";
        }

        public class DisconnectPageRequest
        {
            public string FbUserId { get; set; } = "";
            public string PageID { get; set; } = "";
        }

        public class TogglePageStatusRequest
        {
            public string FbUserId   { get; set; } = "";
            public string PageID     { get; set; } = "";
            public string OpenStatus { get; set; } = "0";
        }

    }
}
