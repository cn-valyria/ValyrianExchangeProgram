import { describe, expect, it, vi } from 'vitest'
import AccountsByAlliance from '~/components/admin/dashboard/AccountsByAlliance.vue'
import AccountsByRole from '~/components/admin/dashboard/AccountsByRole.vue'
import DealAidPerDayTile from '~/components/admin/dashboard/DealAidPerDayTile.vue'
import DealHealthStatCard from '~/components/admin/dashboard/DealHealthStatCard.vue'
import GeneralProgramStatsCard from '~/components/admin/dashboard/GeneralProgramStatsCard.vue'
import OpenSlotsByRole from '~/components/admin/dashboard/OpenSlotsByRole.vue'
import ProgramSlotUsageStatCard from '~/components/admin/dashboard/ProgramSlotUsageStatCard.vue'
import SlotRatiosStatCard from '~/components/admin/dashboard/SlotRatiosStatCard.vue'
import TotalSlotsByAlliance from '~/components/admin/dashboard/TotalSlotsByAlliance.vue'
import { mountInApp } from '../helpers/dom'

// happy-dom has no canvas, so charts are stubbed to expose the data they're given
vi.mock('vue-chartjs', () => import('../helpers/chartStub'))

type ChartData = {
  labels: string[]
  datasets: {
    label?: string
    data: number[]
    backgroundColor?: string | string[]
  }[]
}

const chartData = (wrapper: {
  find: (selector: string) => {
    attributes: (name: string) => string | undefined
  }
}): ChartData =>
  JSON.parse(wrapper.find('[data-chart]').attributes('data-chart-data')!)

describe('stat cards', () => {
  it('show the program totals', async () => {
    const wrapper = await mountInApp(GeneralProgramStatsCard, {
      data: { totalAccounts: 120, activeDeals: 230 },
    })

    expect(wrapper.text()).toContain('120')
    expect(wrapper.text()).toContain('230')
  })

  it('show how many slots are in deals', async () => {
    const wrapper = await mountInApp(ProgramSlotUsageStatCard, {
      data: { maxCapacity: 400, currentSlotsUsed: 300 },
    })

    expect(wrapper.text()).toContain('75.00%')
    expect(wrapper.text()).toContain('(300 / 400)')
  })

  it('show the cash and tech slot ratios', async () => {
    const wrapper = await mountInApp(SlotRatiosStatCard, {
      data: {
        totalIncomingCashSlots: 30,
        totalOutgoingCashSlots: 20,
        totalIncomingTechSlots: 10,
        totalOutgoingTechSlots: 40,
      },
    })

    expect(wrapper.text()).toContain('1.50')
    expect(wrapper.text()).toContain('0.25')
  })

  it('show deal health, highlighting open flags', async () => {
    const wrapper = await mountInApp(DealHealthStatCard, {
      data: { accountsWaiting: 3, slotsWaiting: 7, openFlags: 2 },
    })

    expect(wrapper.text()).toContain('3')
    expect(wrapper.text()).toContain('(7 slots)')
    expect(wrapper.find('.text-red-600').text()).toBe('2')
  })

  it.each([
    [GeneralProgramStatsCard, ['0', '0']],
    [ProgramSlotUsageStatCard, ['0.00%']],
    [SlotRatiosStatCard, ['0.00', '0.00']],
    [DealHealthStatCard, ['0', '0']],
  ])('show zeros before the dashboard loads', async (component, figures) => {
    const wrapper = await mountInApp(component, { data: undefined })

    expect(wrapper.findAll('.text-3xl').map((figure) => figure.text())).toEqual(
      figures,
    )
    expect(wrapper.find('.text-red-600').exists()).toBe(false)
  })
})

describe('AccountsByAlliance', () => {
  it('lists alliances with their account counts', async () => {
    const wrapper = await mountInApp(AccountsByAlliance, {
      data: [
        { allianceName: 'The Imperial Order', totalAccounts: 12 },
        { allianceName: 'Fark', totalAccounts: 3 },
      ],
    })

    expect(wrapper.text()).toContain('TIO')
    expect(wrapper.text()).toContain('The Imperial Order')
    expect(wrapper.text()).toContain('12')
    expect(wrapper.text()).toContain('Fark')
  })

  it('is empty before the dashboard loads', async () => {
    const wrapper = await mountInApp(AccountsByAlliance, { data: undefined })

    expect(wrapper.text()).toBe('Most Accounts')
  })
})

describe('charts', () => {
  it('chart accounts by role, labelled with role names', async () => {
    const wrapper = await mountInApp(AccountsByRole, {
      data: [
        { role: 'B', totalAccounts: 5 },
        { role: 'S', totalAccounts: 7 },
        { role: '?', totalAccounts: 1 },
      ],
    })

    const data = chartData(wrapper)
    expect(data.labels).toEqual(['Buyer', 'Seller', ''])
    expect(data.datasets[0].data).toEqual([5, 7, 1])
  })

  it('chart open slots by role, keeping unknown role codes', async () => {
    const wrapper = await mountInApp(OpenSlotsByRole, {
      data: [
        { role: 'D', openSlots: 4 },
        { role: 'X', openSlots: 1 },
      ],
    })

    const data = chartData(wrapper)
    expect(data.labels).toEqual(['Cash Donor', 'X'])
    expect(data.datasets[0].data).toEqual([4, 1])
    expect(data.datasets[0].backgroundColor).toHaveLength(2)
  })

  it('chart deal aid per day', async () => {
    const wrapper = await mountInApp(DealAidPerDayTile, {
      data: [
        { day: '2026-10-08T12:00:00', aidCount: 3 },
        { day: '2026-10-09T12:00:00', aidCount: 5 },
      ],
    })

    const data = chartData(wrapper)
    expect(data.labels).toEqual(['Oct-08', 'Oct-09'])
    expect(data.datasets[0].data).toEqual([3, 5])
  })

  it('stack slots per aid direction by alliance acronym', async () => {
    vi.spyOn(console, 'log').mockImplementation(() => {})
    const wrapper = await mountInApp(TotalSlotsByAlliance, {
      alliances: ['The Imperial Order', 'Fark'],
      data: [
        {
          slotGroupName: 'Incoming Cash',
          totalSlotGroups: [
            { allianceName: 'The Imperial Order', totalSlots: 4 },
          ],
        },
        {
          slotGroupName: 'Outgoing Cash',
          totalSlotGroups: [
            { allianceName: 'Fark', totalSlots: 2 },
            { allianceName: 'The Imperial Order', totalSlots: 1 },
          ],
        },
      ],
    })

    const data = chartData(wrapper)
    expect(data.labels).toEqual(['Incoming Cash', 'Outgoing Cash'])
    expect(data.datasets.map((d) => [d.label, d.data])).toEqual([
      ['TIO', [4, 1]],
      ['FARK', [0, 2]],
    ])
  })

  it.each([
    [AccountsByRole, {}],
    [OpenSlotsByRole, {}],
    [DealAidPerDayTile, {}],
    [TotalSlotsByAlliance, { alliances: [] }],
  ])(
    'draw an empty chart before the dashboard loads',
    async (component, props) => {
      const wrapper = await mountInApp(component, { data: undefined, ...props })

      expect(chartData(wrapper).labels).toEqual([])
    },
  )
})
