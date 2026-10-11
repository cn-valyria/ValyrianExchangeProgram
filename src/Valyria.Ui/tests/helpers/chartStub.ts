import { defineComponent, h } from 'vue'

// happy-dom has no canvas, so tests swap vue-chartjs for these, which expose the data each chart is given:
// vi.mock('vue-chartjs', () => import('../helpers/chartStub'))
const chart = (type: string) =>
  defineComponent({
    props: ['data', 'options'],
    setup: (props) => () =>
      h('div', {
        'data-chart': type,
        'data-chart-data': JSON.stringify(props.data),
        'data-chart-options': JSON.stringify(props.options),
      }),
  })

export const Bar = chart('bar')
export const Line = chart('line')
export const Pie = chart('pie')
